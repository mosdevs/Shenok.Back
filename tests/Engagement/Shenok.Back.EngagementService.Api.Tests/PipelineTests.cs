using System.Net;
using System.Net.Http.Json;
using Shenok.Back.Api.Shared.Correlation;

namespace Shenok.Back.EngagementService.Api.Tests;

[Collection("Engagement API")]
public sealed class PipelineTests(EngagementApiFixture api)
{
    [Fact]
    public async Task Health_is_open_without_the_internal_key()
    {
        var response = await api.SendAsync(HttpMethod.Get, "/health", apiKey: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_internal_key_on_scans_is_401()
    {
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/scans",
            ScanBody(),
            apiKey: false,
            userId: Guid.NewGuid(),
            correlationId: "corr-missing-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("corr-missing-key", response.Headers.GetValues(CorrelationId.HeaderName).Single());
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.Unauthorized, "https://shenok.dev/problems/unauthorized");
        Assert.Equal("corr-missing-key", body.GetProperty("traceId").GetString());
        Assert.Equal("The internal API key is missing.", body.GetProperty("detail").GetString());
        Assert.False(body.TryGetProperty("unlocked", out _));
    }

    [Fact]
    public async Task Wrong_internal_key_is_401_and_health_stays_open()
    {
        var health = new HttpRequestMessage(HttpMethod.Get, "/health");
        health.Headers.TryAddWithoutValidation("X-Internal-Key", "not-the-key");
        var open = await api.Client.SendAsync(health);
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);

        var scans = new HttpRequestMessage(HttpMethod.Post, "/internal/scans");
        scans.Headers.TryAddWithoutValidation("X-Internal-Key", "not-the-key");
        scans.Headers.TryAddWithoutValidation("X-User-Id", Guid.NewGuid().ToString("D"));
        var response = await api.Client.SendAsync(scans);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.Unauthorized, "https://shenok.dev/problems/unauthorized");
        Assert.Equal("The internal API key is missing.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Missing_user_id_is_401()
    {
        var response = await api.SendAsync(HttpMethod.Post, "/internal/scans", ScanBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.Unauthorized, "https://shenok.dev/problems/unauthorized");
        Assert.Equal("The caller id is missing.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Empty_user_id_is_401()
    {
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/can-play",
            new { pageId = Guid.NewGuid() },
            userId: Guid.Empty);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.Unauthorized, "https://shenok.dev/problems/unauthorized");
        Assert.Equal("The caller id is missing.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Root_is_not_a_public_entry()
    {
        var anonymous = await api.SendAsync(HttpMethod.Get, "/", apiKey: false);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var signed = await api.SendAsync(HttpMethod.Get, "/", userId: Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, signed.StatusCode);
    }

    [Fact]
    public void Test_host_does_not_register_postgres_or_an_actor_system()
    {
        Assert.False(api.RegistersDatabase);
        Assert.False(api.RegistersActorSystem);
    }

    [Fact]
    public async Task Ready_is_open_without_the_internal_key()
    {
        var response = await api.SendAsync(HttpMethod.Get, "/ready", apiKey: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Swagger_is_not_mounted()
    {
        var index = await api.SendAsync(HttpMethod.Get, "/swagger", apiKey: false);
        var document = await api.SendAsync(HttpMethod.Get, "/swagger/v1/swagger.json", apiKey: false);

        Assert.Equal(HttpStatusCode.NotFound, index.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
    }

    [Fact]
    public async Task Malformed_json_is_validation_400()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/scans");
        request.Headers.TryAddWithoutValidation("X-Internal-Key", EngagementApiFixture.ApiKey);
        request.Headers.TryAddWithoutValidation("X-User-Id", Guid.NewGuid().ToString("D"));
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "corr-malformed");
        request.Content = new StringContent("{", System.Text.Encoding.UTF8, "application/json");

        var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("corr-malformed", response.Headers.GetValues(CorrelationId.HeaderName).Single());
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.BadRequest, "https://shenok.dev/problems/validation");
        Assert.Equal("corr-malformed", body.GetProperty("traceId").GetString());
        Assert.NotEqual("https://shenok.dev/problems/server", body.GetProperty("type").GetString());
        Assert.True(body.GetProperty("errors").EnumerateObject().Any());
    }

    [Fact]
    public async Task Omitted_scan_body_is_validation_400()
    {
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/scans",
            userId: Guid.NewGuid(),
            correlationId: "corr-empty-body");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("corr-empty-body", response.Headers.GetValues(CorrelationId.HeaderName).Single());
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.BadRequest, "https://shenok.dev/problems/validation");
        Assert.Equal("corr-empty-body", body.GetProperty("traceId").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("ScanId", out _));
        Assert.True(body.GetProperty("errors").TryGetProperty("PageId", out _));
    }

    [Fact]
    public async Task Non_guid_user_id_is_401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/can-play");
        request.Headers.TryAddWithoutValidation("X-Internal-Key", EngagementApiFixture.ApiKey);
        request.Headers.TryAddWithoutValidation("X-User-Id", "not-a-guid");
        request.Content = JsonContent.Create(new { pageId = Guid.NewGuid() });

        var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.Unauthorized, "https://shenok.dev/problems/unauthorized");
        Assert.Equal("The caller id is missing.", body.GetProperty("detail").GetString());
    }

    private static object ScanBody() => new
    {
        scanId = "scan-pipeline",
        waveformId = Guid.NewGuid(),
        voiceId = Guid.NewGuid(),
        pageId = Guid.NewGuid()
    };
}
