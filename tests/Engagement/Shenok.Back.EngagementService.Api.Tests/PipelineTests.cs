using System.Net;
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
        ApiJson.AssertProblem(body, HttpStatusCode.Unauthorized, "https://shenok.dev/problems/unauthorized");
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
        Assert.Equal("The internal API key is missing.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Missing_user_id_is_401()
    {
        var response = await api.SendAsync(HttpMethod.Post, "/internal/scans", ScanBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(body, HttpStatusCode.Unauthorized, "https://shenok.dev/problems/unauthorized");
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

    private static object ScanBody() => new
    {
        scanId = "scan-pipeline",
        waveformId = Guid.NewGuid(),
        voiceId = Guid.NewGuid(),
        pageId = Guid.NewGuid()
    };
}
