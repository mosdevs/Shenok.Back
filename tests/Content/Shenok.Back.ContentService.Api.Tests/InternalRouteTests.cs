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
    public async Task Subscriber_and_play_commands_answer_501_and_do_not_require_a_user()
    {
        var page = api.SeedPublished();
        var voiceId = page.VoiceIds[0].Value;

        var subscribers = await api.SendAsync(
            HttpMethod.Post,
            $"/internal/pages/{page.Id.Value:D}/subscribers",
            new { delta = 1, idempotencyKey = "follow-1" });
        Assert.Equal(HttpStatusCode.NotImplemented, subscribers.StatusCode);
        Assert.Equal("application/problem+json", subscribers.Content.Headers.ContentType?.MediaType);
        var subscriberProblem = await ApiJson.ReadAsync(subscribers);
        ApiJson.AssertProblem(subscriberProblem, HttpStatusCode.NotImplemented, "https://shenok.dev/problems/not-implemented");
        Assert.Equal("Content counters are applied in phase 6 and were not changed.", subscriberProblem.GetProperty("detail").GetString());

        var plays = await api.SendAsync(
            HttpMethod.Post,
            $"/internal/voices/{voiceId:D}/plays",
            new { playSessionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotImplemented, plays.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(plays), HttpStatusCode.NotImplemented, "https://shenok.dev/problems/not-implemented");

        var invalidDelta = await api.SendAsync(
            HttpMethod.Post,
            $"/internal/pages/{page.Id.Value:D}/subscribers",
            new { delta = 2, idempotencyKey = "follow-2" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidDelta.StatusCode);
        ApiJson.AssertProblem(await ApiJson.ReadAsync(invalidDelta), HttpStatusCode.BadRequest, "https://shenok.dev/problems/validation");

        var catalog = await api.SendAsync(HttpMethod.Get, $"/internal/pages/{page.Id.Value:D}");
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
        Assert.Equal("Published", (await ApiJson.ReadAsync(catalog)).GetProperty("status").GetString());
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
