using System.Net;

namespace Shenok.Back.ContentService.Api.Tests;

[Collection("Content API")]
public sealed class InternalRouteTests(ContentApiFixture api)
{
    [Fact]
    public async Task Playback_without_a_scan_returns_only_signed_segment_urls()
    {
        var page = api.SeedPublished();
        var voiceId = page.VoiceIds[0].Value;

        var response = await api.SendAsync(HttpMethod.Post, "/internal/playback", new
        {
            voiceId,
            waveformId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        var playlist = body.GetProperty("playlist").GetString() ?? string.Empty;
        AssertSignedPlaylist(playlist, voiceId);
        Assert.Equal(page.Id.Value, body.GetProperty("pageId").GetGuid());
        Assert.True(body.GetProperty("expiresUnixSeconds").GetInt64() > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Revoked_waveform_is_rejected_for_scan_playback()
    {
        var page = api.SeedPublished();
        var mapping = api.SeedPageWaveform(page, revoke: true);

        var response = await api.SendAsync(HttpMethod.Post, "/internal/playback", new
        {
            voiceId = page.VoiceIds[0].Value,
            waveformId = mapping.Id.Value
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(problem, HttpStatusCode.Conflict, "https://shenok.dev/problems/conflict");
        Assert.Contains("not active", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Active_scan_playback_is_signed()
    {
        var page = api.SeedPublished();
        var mapping = api.SeedPageWaveform(page);
        var voiceId = page.VoiceIds[0].Value;

        var response = await api.SendAsync(HttpMethod.Post, "/internal/playback", new
        {
            voiceId,
            waveformId = mapping.Id.Value
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var playlist = (await ApiJson.ReadAsync(response)).GetProperty("playlist").GetString() ?? string.Empty;
        AssertSignedPlaylist(playlist, voiceId);
    }

    [Fact]
    public async Task Subscriber_and_play_commands_apply_once_and_do_not_require_a_user()
    {
        var page = api.SeedPublished();
        var voiceId = page.VoiceIds[0].Value;
        var playSessionId = Guid.NewGuid();

        var subscribers = await api.SendAsync(
            HttpMethod.Post,
            $"/internal/pages/{page.Id.Value:D}/subscribers",
            new { delta = 1, idempotencyKey = "follow-1" });
        Assert.Equal(HttpStatusCode.OK, subscribers.StatusCode);
        var subscriberBody = await ApiJson.ReadAsync(subscribers);
        Assert.True(subscriberBody.GetProperty("applied").GetBoolean());
        Assert.Equal(1, subscriberBody.GetProperty("subscriberCount").GetInt64());

        var replay = await api.SendAsync(
            HttpMethod.Post,
            $"/internal/pages/{page.Id.Value:D}/subscribers",
            new { delta = 1, idempotencyKey = "follow-1" });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayBody = await ApiJson.ReadAsync(replay);
        Assert.False(replayBody.GetProperty("applied").GetBoolean());
        Assert.Equal(1, replayBody.GetProperty("subscriberCount").GetInt64());

        var plays = await api.SendAsync(
            HttpMethod.Post,
            $"/internal/voices/{voiceId:D}/plays",
            new { playSessionId });
        Assert.Equal(HttpStatusCode.OK, plays.StatusCode);
        var playBody = await ApiJson.ReadAsync(plays);
        Assert.True(playBody.GetProperty("applied").GetBoolean());
        Assert.Equal(1, playBody.GetProperty("playCount").GetInt64());

        var playReplay = await api.SendAsync(
            HttpMethod.Post,
            $"/internal/voices/{voiceId:D}/plays",
            new { playSessionId });
        Assert.Equal(HttpStatusCode.OK, playReplay.StatusCode);
        Assert.False((await ApiJson.ReadAsync(playReplay)).GetProperty("applied").GetBoolean());

        var invalidDelta = await api.SendAsync(
            HttpMethod.Post,
            $"/internal/pages/{page.Id.Value:D}/subscribers",
            new { delta = 2, idempotencyKey = "follow-2" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidDelta.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(invalidDelta), HttpStatusCode.BadRequest, "https://shenok.dev/problems/validation");

        var catalog = await api.SendAsync(HttpMethod.Get, $"/internal/pages/{page.Id.Value:D}");
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
        var catalogBody = await ApiJson.ReadAsync(catalog);
        Assert.Equal("Published", catalogBody.GetProperty("status").GetString());
        Assert.Equal(1, catalogBody.GetProperty("subscriberCount").GetInt64());
        var listed = catalogBody.GetProperty("voices").EnumerateArray().Single();
        Assert.Equal(voiceId, listed.GetProperty("voiceId").GetGuid());
        Assert.Equal(1, listed.GetProperty("playCount").GetInt64());
    }

    [Fact]
    public async Task Creator_catalog_does_not_treat_a_stranger_as_the_owner()
    {
        var page = api.SeedDraft();

        var response = await api.AsCreatorAsync(
            HttpMethod.Get,
            $"/pages/{page.Id.Value:D}",
            userId: Guid.NewGuid(),
            roles: "ContentCreator");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Ready_voice_segment_is_streamed_without_a_user_id()
    {
        var page = api.SeedPublished();
        var voiceId = page.VoiceIds[0].Value;
        var payload = new byte[] { 0x47, 9, 8, 7 };
        api.Blobs.Objects["hls/seg/seg000.aac"] = payload;

        var relative = await api.SendAsync(HttpMethod.Get, Segment(voiceId, "seg000.aac"));
        var prefixed = await api.SendAsync(HttpMethod.Get, Segment(voiceId, "hls/seg/seg000.aac"));

        Assert.Equal(HttpStatusCode.OK, relative.StatusCode);
        Assert.Equal(HttpStatusCode.OK, prefixed.StatusCode);
        Assert.Equal("video/mp2t", relative.Content.Headers.ContentType?.MediaType);
        Assert.Equal(payload.LongLength, relative.Content.Headers.ContentLength);
        Assert.Equal(payload, await relative.Content.ReadAsByteArrayAsync());
        Assert.Equal(payload, await prefixed.Content.ReadAsByteArrayAsync());
        Assert.Equal(
            ["hls/seg/seg000.aac", "hls/seg/seg000.aac"],
            api.Blobs.Opens.TakeLast(2).ToArray());
    }

    [Theory]
    [InlineData("packages/other-voice/seg_00000.ts")]
    [InlineData("hls/other/seg.ts")]
    [InlineData("seg%3Fname.ts")]
    [InlineData("seg%23name.ts")]
    public async Task A_bad_segment_path_is_rejected_without_calling_storage(string path)
    {
        var page = api.SeedPublished();
        var before = api.Blobs.Opens.Count;

        var response = await api.SendAsync(HttpMethod.Get, Segment(page.VoiceIds[0].Value, path));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(problem, HttpStatusCode.Conflict, "https://shenok.dev/problems/conflict");
        Assert.Equal("Segment path is invalid.", problem.GetProperty("detail").GetString());
        Assert.Equal(before, api.Blobs.Opens.Count);
    }

    [Fact]
    public async Task A_missing_segment_is_not_found()
    {
        var page = api.SeedPublished();
        var before = api.Blobs.Opens.Count;

        var response = await api.SendAsync(HttpMethod.Get, Segment(page.VoiceIds[0].Value, "missing.ts"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(response), HttpStatusCode.NotFound, "https://shenok.dev/problems/not-found");
        Assert.Equal(before + 1, api.Blobs.Opens.Count);
        Assert.Equal("hls/seg/missing.ts", api.Blobs.Opens[^1]);
    }

    [Fact]
    public async Task A_voice_that_is_not_ready_is_rejected_without_calling_storage()
    {
        var page = api.SeedDraft();
        var pending = api.SeedPending(page);
        var failed = api.SeedPending(page, "Broken");
        failed.CompletePackagingFailure();
        var before = api.Blobs.Opens.Count;

        var pendingResponse = await api.SendAsync(HttpMethod.Get, Segment(pending.Id.Value, "seg.ts"));
        var failedResponse = await api.SendAsync(HttpMethod.Get, Segment(failed.Id.Value, "seg.ts"));

        Assert.Equal(HttpStatusCode.BadRequest, pendingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, failedResponse.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(pendingResponse), HttpStatusCode.BadRequest, "https://shenok.dev/problems/domain");
        ApiJson.AssertProblem(await ApiJson.ReadAsync(failedResponse), HttpStatusCode.BadRequest, "https://shenok.dev/problems/domain");
        Assert.Equal(before, api.Blobs.Opens.Count);
    }

    private static string Segment(Guid voiceId, string path) =>
        $"/internal/voices/{voiceId:D}/segments/{path}";

    private static void AssertSignedPlaylist(string playlist, Guid voiceId)
    {
        Assert.StartsWith("#EXTM3U", playlist, StringComparison.Ordinal);
        Assert.Contains($"{ContentApiFixture.GatewayBaseUrl}/audio/{voiceId:D}/segments/seg000.aac?exp=", playlist, StringComparison.Ordinal);
        Assert.Contains("&sig=", playlist, StringComparison.Ordinal);
        Assert.DoesNotContain("\nseg000.aac\n", playlist, StringComparison.Ordinal);
        Assert.DoesNotContain("hls/seg/", playlist, StringComparison.Ordinal);
        Assert.DoesNotContain("hls/playlist.m3u8", playlist, StringComparison.Ordinal);
    }
}
