using System.Net;
using System.Text.Json;
using Shenok.Back.Gateway.Api;
using Shenok.Back.Gateway.Api.Security;

namespace Shenok.Back.Gateway.Api.Tests;

[Collection("Gateway API")]
public sealed class PipelineTests(GatewayApiFixture api)
{
    [Fact]
    public async Task Health_is_open_without_a_token_and_does_not_call_downstream()
    {
        var before = DownstreamCalls();

        var response = await api.SendAsync(HttpMethod.Get, "/health", correlationId: "corr-health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("corr-health", response.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(before, DownstreamCalls());
    }

    [Fact]
    public async Task Health_ignores_an_inbound_internal_key_and_a_bad_token()
    {
        var before = DownstreamCalls();

        var response = await api.SendAsync(
            HttpMethod.Get,
            "/health",
            bearer: "not-a-jwt",
            internalKey: "not-the-key");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, DownstreamCalls());
    }

    [Fact]
    public async Task Unknown_path_is_401_until_the_caller_is_verified()
    {
        using var anonymous = await api.SendAsync(HttpMethod.Get, "/missing");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var token = TestTokens.Create(Guid.NewGuid());
        using var signed = await api.SendAsync(HttpMethod.Get, "/missing", bearer: token);
        Assert.Equal(HttpStatusCode.NotFound, signed.StatusCode);
    }

    [Fact]
    public async Task Probe_without_a_token_is_401_problem_json()
    {
        var response = await api.SendAsync(HttpMethod.Get, "/probe", correlationId: "corr-anon");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("corr-anon", response.Headers.GetValues("X-Correlation-Id").Single());
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.Unauthorized, "https://shenok.dev/problems/unauthorized");
        Assert.Equal("corr-anon", body.GetProperty("traceId").GetString());
        Assert.Equal("The access token is missing or invalid.", body.GetProperty("detail").GetString());
        Assert.DoesNotContain("internal API key", body.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Probe_with_a_bad_token_is_401_and_does_not_echo_the_token()
    {
        const string token = "this-is-not-a-jwt";
        var response = await api.SendAsync(HttpMethod.Get, "/probe", bearer: token);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(token, raw, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(raw);
        Assert.Equal("The access token is missing or invalid.", body.RootElement.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("false")]
    [InlineData("TRUE")]
    [InlineData("")]
    public async Task Token_that_is_not_verified_true_is_403(string verified)
    {
        var token = TestTokens.Create(Guid.NewGuid(), verified: verified);
        var response = await api.SendAsync(HttpMethod.Get, "/probe/named", bearer: token);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(token, raw, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(raw);
        ApiJson.AssertProblem(response, body.RootElement, HttpStatusCode.Forbidden, "https://shenok.dev/problems/forbidden");
        Assert.Equal("The caller is not verified.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Token_without_a_verified_claim_is_403()
    {
        var token = TestTokens.Create(Guid.NewGuid(), verified: null);
        var response = await api.SendAsync(HttpMethod.Get, "/probe", bearer: token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        Assert.Equal("The caller is not verified.", body.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("/probe")]
    [InlineData("/probe/named")]
    public async Task Verified_token_keeps_sub_and_does_not_need_the_internal_key(string path)
    {
        var userId = Guid.NewGuid();
        var token = TestTokens.Create(userId, roles: ["Listener"]);
        var response = await api.SendAsync(HttpMethod.Get, path, bearer: token, internalKey: "ignored");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        Assert.Equal(userId.ToString("D"), body.GetProperty("sub").GetString());
        Assert.Equal(GatewayClaims.VerifiedTrue, body.GetProperty("verified").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("mappedSub").ValueKind);
    }

    [Fact]
    public async Task Expired_token_is_401()
    {
        var token = TestTokens.Create(
            Guid.NewGuid(),
            expires: DateTime.UtcNow - GatewayConfiguration.JwtClockSkew - TimeSpan.FromMinutes(1));
        var response = await api.SendAsync(HttpMethod.Get, "/probe", bearer: token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        Assert.Equal("The access token is missing or invalid.", body.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData("other-issuer", "shenok-mobile", null)]
    [InlineData("shenok", "other-audience", null)]
    [InlineData("shenok", "shenok-mobile", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-other")]
    public async Task Token_with_the_wrong_issuer_audience_or_key_is_401(string issuer, string audience, string? signingKey)
    {
        var token = TestTokens.Create(Guid.NewGuid(), issuer: issuer, audience: audience, signingKey: signingKey);
        var response = await api.SendAsync(HttpMethod.Get, "/probe", bearer: token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_without_a_guid_subject_is_401()
    {
        var missing = TestTokens.Create(Guid.NewGuid(), includeSubject: false);
        var empty = TestTokens.Create(Guid.NewGuid(), subject: Guid.Empty.ToString("D"));
        var text = TestTokens.Create(Guid.NewGuid(), subject: "not-a-guid");

        using var missingResponse = await api.SendAsync(HttpMethod.Get, "/probe", bearer: missing);
        using var emptyResponse = await api.SendAsync(HttpMethod.Get, "/probe", bearer: empty);
        using var textResponse = await api.SendAsync(HttpMethod.Get, "/probe", bearer: text);

        Assert.Equal(HttpStatusCode.Unauthorized, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, emptyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, textResponse.StatusCode);
        var body = await ApiJson.ReadAsync(textResponse);
        Assert.Equal("The access token is missing or invalid.", body.GetProperty("detail").GetString());
        Assert.DoesNotContain("not-a-guid", body.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    private int DownstreamCalls() => api.Identity.Calls + api.Content.Calls + api.Engagement.Calls;
}
