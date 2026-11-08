using System.Net;
using Shenok.Back.EngagementService.Application.Common;

namespace Shenok.Back.EngagementService.Api.Tests;

[Collection("Engagement API")]
public sealed class ListeningRouteTests(EngagementApiFixture api)
{
    [Fact]
    public async Task Can_play_is_false_before_a_scan_and_true_after()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var voiceId = Guid.NewGuid();

        var before = await api.SendAsync(HttpMethod.Post, "/internal/can-play", new { pageId }, userId: userId);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.False((await ApiJson.ReadAsync(before)).GetProperty("canPlay").GetBoolean());

        var scan = await api.SendAsync(
            HttpMethod.Post,
            "/internal/scans",
            new
            {
                scanId = Guid.NewGuid().ToString("N"),
                waveformId = Guid.NewGuid(),
                voiceId,
                pageId
            },
            userId: userId);
        Assert.Equal(HttpStatusCode.OK, scan.StatusCode);

        var after = await api.SendAsync(HttpMethod.Post, "/internal/can-play", new { pageId }, userId: userId);
        Assert.True((await ApiJson.ReadAsync(after)).GetProperty("canPlay").GetBoolean());
    }

    [Fact]
    public async Task Listening_state_fills_missing_voices_and_keeps_duplicate_order()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var known = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString("N");

        await api.SendAsync(
            HttpMethod.Post,
            "/internal/scans",
            new
            {
                scanId = Guid.NewGuid().ToString("N"),
                waveformId = Guid.NewGuid(),
                voiceId = known,
                pageId
            },
            userId: userId);
        var progress = await api.SendAsync(
            HttpMethod.Post,
            "/internal/progress",
            new
            {
                voiceId = known,
                playSessionId = sessionId,
                positionSeconds = 12,
                completed = true,
                isSeek = false
            },
            userId: userId);
        Assert.Equal(HttpStatusCode.OK, progress.StatusCode);

        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/listening-state",
            new { pageId, voiceIds = new[] { missing, known, missing } },
            userId: userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        Assert.False(body.GetProperty("isFollowing").GetBoolean());
        Assert.True(body.GetProperty("unlocked").GetBoolean());
        var voices = body.GetProperty("voices").EnumerateArray().ToArray();
        Assert.Equal(3, voices.Length);
        Assert.Equal(missing, voices[0].GetProperty("voiceId").GetGuid());
        Assert.Equal(0, voices[0].GetProperty("positionSeconds").GetInt32());
        Assert.False(voices[0].GetProperty("isCompleted").GetBoolean());
        Assert.Equal(known, voices[1].GetProperty("voiceId").GetGuid());
        Assert.Equal(12, voices[1].GetProperty("positionSeconds").GetInt32());
        Assert.True(voices[1].GetProperty("isCompleted").GetBoolean());
        Assert.Equal(missing, voices[2].GetProperty("voiceId").GetGuid());
        Assert.Equal(0, voices[2].GetProperty("positionSeconds").GetInt32());
    }

    [Fact]
    public async Task Empty_voice_list_returns_the_flags_only()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/listening-state",
            new { pageId, voiceIds = Array.Empty<Guid>() },
            userId: userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        Assert.False(body.GetProperty("isFollowing").GetBoolean());
        Assert.False(body.GetProperty("unlocked").GetBoolean());
        Assert.Empty(body.GetProperty("voices").EnumerateArray());
    }

    [Fact]
    public async Task More_than_500_voice_ids_is_validation_400()
    {
        var voiceIds = Enumerable.Range(0, EngagementLimits.MaxVoicesPerRequest + 1).Select(_ => Guid.NewGuid()).ToArray();
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/listening-state",
            new { pageId = Guid.NewGuid(), voiceIds },
            userId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(body, HttpStatusCode.BadRequest, "https://shenok.dev/problems/validation");
        var messages = body.GetProperty("errors").GetProperty("VoiceIds").EnumerateArray().Select(item => item.GetString());
        Assert.Contains(EngagementLimits.TooManyVoicesMessage, messages);
    }
}
