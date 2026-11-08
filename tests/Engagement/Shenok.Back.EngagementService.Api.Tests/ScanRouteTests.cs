using System.Net;
using Shenok.Back.EngagementService.Application.Common;

namespace Shenok.Back.EngagementService.Api.Tests;

[Collection("Engagement API")]
public sealed class ScanRouteTests(EngagementApiFixture api)
{
    [Fact]
    public async Task Post_scan_returns_the_unlock_snapshot()
    {
        var userId = Guid.NewGuid();
        var response = await api.SendAsync(HttpMethod.Post, "/internal/scans", Scan(), userId: userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        Assert.True(body.GetProperty("unlocked").GetBoolean());
        Assert.False(body.GetProperty("isFollowing").GetBoolean());
        Assert.True(body.GetProperty("promptFollow").GetBoolean());
        Assert.Equal(0, body.GetProperty("resumePositionSeconds").GetInt32());
    }

    [Fact]
    public async Task Repeated_scan_id_returns_the_stored_snapshot()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var voiceId = Guid.NewGuid();
        var scanId = NewToken();
        var scan = Scan(scanId, pageId, voiceId);

        var first = await ApiJson.ReadAsync(await api.SendAsync(HttpMethod.Post, "/internal/scans", scan, userId: userId));
        await api.SendAsync(
            HttpMethod.Post,
            "/internal/progress",
            new
            {
                voiceId,
                playSessionId = NewToken(),
                positionSeconds = 20,
                completed = false,
                isSeek = false
            },
            userId: userId);
        await api.SendAsync(HttpMethod.Post, $"/internal/pages/{pageId:D}/follow", userId: userId);

        var repeated = await api.SendAsync(HttpMethod.Post, "/internal/scans", scan, userId: userId);

        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var second = await ApiJson.ReadAsync(repeated);
        Assert.Equal(first.GetProperty("unlocked").GetBoolean(), second.GetProperty("unlocked").GetBoolean());
        Assert.Equal(first.GetProperty("isFollowing").GetBoolean(), second.GetProperty("isFollowing").GetBoolean());
        Assert.Equal(first.GetProperty("promptFollow").GetBoolean(), second.GetProperty("promptFollow").GetBoolean());
        Assert.Equal(0, second.GetProperty("resumePositionSeconds").GetInt32());
        Assert.False(second.GetProperty("isFollowing").GetBoolean());
        Assert.True(second.GetProperty("promptFollow").GetBoolean());
    }

    [Fact]
    public async Task Scan_id_owned_by_another_user_is_409_without_the_snapshot()
    {
        var owner = Guid.NewGuid();
        var scanId = NewToken();
        var stored = await api.SendAsync(HttpMethod.Post, "/internal/scans", Scan(scanId), userId: owner);
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);

        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/scans",
            Scan(scanId),
            userId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.Conflict, "https://shenok.dev/problems/conflict");
        Assert.Equal(EngagementConflict.ScanBelongsToAnotherUser, body.GetProperty("detail").GetString());
        Assert.False(body.TryGetProperty("unlocked", out _));
        Assert.False(body.TryGetProperty("isFollowing", out _));
        Assert.False(body.TryGetProperty("promptFollow", out _));
        Assert.False(body.TryGetProperty("resumePositionSeconds", out _));
    }

    [Fact]
    public async Task Body_user_id_does_not_replace_the_header()
    {
        var headerUser = Guid.NewGuid();
        var bodyUser = Guid.NewGuid();
        var scanId = NewToken();
        var scan = new
        {
            scanId,
            userId = bodyUser,
            waveformId = Guid.NewGuid(),
            voiceId = Guid.NewGuid(),
            pageId = Guid.NewGuid()
        };

        var stored = await api.SendAsync(HttpMethod.Post, "/internal/scans", scan, userId: headerUser);
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);

        var asBodyUser = await api.SendAsync(HttpMethod.Post, "/internal/scans", scan, userId: bodyUser);
        Assert.Equal(HttpStatusCode.Conflict, asBodyUser.StatusCode);

        var asHeaderUser = await api.SendAsync(HttpMethod.Post, "/internal/scans", scan, userId: headerUser);
        Assert.Equal(HttpStatusCode.OK, asHeaderUser.StatusCode);
        Assert.True((await ApiJson.ReadAsync(asHeaderUser)).GetProperty("unlocked").GetBoolean());
    }

    [Fact]
    public async Task Empty_page_id_is_validation_400()
    {
        var userId = Guid.NewGuid();
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/scans",
            new
            {
                scanId = NewToken(),
                waveformId = Guid.NewGuid(),
                voiceId = Guid.NewGuid(),
                pageId = Guid.Empty
            },
            userId: userId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.BadRequest, "https://shenok.dev/problems/validation");
        Assert.True(body.GetProperty("errors").TryGetProperty("PageId", out _));
        Assert.NotEqual("https://shenok.dev/problems/server", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Blank_scan_id_is_validation_400()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/scans",
            new
            {
                scanId = "   ",
                waveformId = Guid.NewGuid(),
                voiceId = Guid.NewGuid(),
                pageId
            },
            userId: userId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ApiJson.ReadAsync(response);
        Assert.True(problem.GetProperty("errors").TryGetProperty("ScanId", out _));

        var playback = await api.SendAsync(
            HttpMethod.Post,
            "/internal/can-play",
            new { pageId },
            userId: userId);
        Assert.Equal(HttpStatusCode.OK, playback.StatusCode);
        Assert.False((await ApiJson.ReadAsync(playback)).GetProperty("canPlay").GetBoolean());
    }

    [Fact]
    public async Task Scan_id_longer_than_64_characters_is_validation_400()
    {
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/scans",
            new
            {
                scanId = new string('a', 65),
                waveformId = Guid.NewGuid(),
                voiceId = Guid.NewGuid(),
                pageId = Guid.NewGuid()
            },
            userId: Guid.NewGuid(),
            correlationId: "corr-long-scan");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("corr-long-scan", response.Headers.GetValues("X-Correlation-Id").Single());
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.BadRequest, "https://shenok.dev/problems/validation");
        Assert.Equal("corr-long-scan", body.GetProperty("traceId").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("ScanId", out _));
        Assert.NotEqual("https://shenok.dev/problems/domain", body.GetProperty("type").GetString());
    }

    private static string NewToken() => Guid.NewGuid().ToString("N");

    private static object Scan(string? scanId = null, Guid? pageId = null, Guid? voiceId = null) => new
    {
        scanId = scanId ?? NewToken(),
        waveformId = Guid.NewGuid(),
        voiceId = voiceId ?? Guid.NewGuid(),
        pageId = pageId ?? Guid.NewGuid()
    };
}
