using System.Net;

namespace Shenok.Back.EngagementService.Api.Tests;

[Collection("Engagement API")]
public sealed class FollowRouteTests(EngagementApiFixture api)
{
    [Fact]
    public async Task Follow_sets_following_and_a_second_follow_stays_successful()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        await ScanAsync(userId, pageId);

        var first = await api.SendAsync(HttpMethod.Post, $"/internal/pages/{pageId:D}/follow", userId: userId);
        var second = await api.SendAsync(HttpMethod.Post, $"/internal/pages/{pageId:D}/follow", userId: userId);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(string.Empty, (await first.Content.ReadAsStringAsync()).Trim());
        var state = await ListeningAsync(userId, pageId);
        Assert.True(state.GetProperty("isFollowing").GetBoolean());
        Assert.True(state.GetProperty("unlocked").GetBoolean());
    }

    [Fact]
    public async Task Unfollow_clears_following_and_keeps_the_unlock()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        await ScanAsync(userId, pageId);
        await api.SendAsync(HttpMethod.Post, $"/internal/pages/{pageId:D}/follow", userId: userId);

        var response = await api.SendAsync(HttpMethod.Delete, $"/internal/pages/{pageId:D}/follow", userId: userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await ListeningAsync(userId, pageId);
        Assert.False(state.GetProperty("isFollowing").GetBoolean());
        Assert.True(state.GetProperty("unlocked").GetBoolean());
        var playback = await api.SendAsync(HttpMethod.Post, "/internal/can-play", new { pageId }, userId: userId);
        Assert.True((await ApiJson.ReadAsync(playback)).GetProperty("canPlay").GetBoolean());
    }

    [Fact]
    public async Task Unfollow_of_a_missing_relationship_is_200()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();

        var response = await api.SendAsync(HttpMethod.Delete, $"/internal/pages/{pageId:D}/follow", userId: userId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var playback = await api.SendAsync(HttpMethod.Post, "/internal/can-play", new { pageId }, userId: userId);
        Assert.False((await ApiJson.ReadAsync(playback)).GetProperty("canPlay").GetBoolean());
    }

    [Fact]
    public async Task Empty_page_id_on_follow_is_validation_400()
    {
        var response = await api.SendAsync(
            HttpMethod.Post,
            $"/internal/pages/{Guid.Empty:D}/follow",
            userId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(response, body, HttpStatusCode.BadRequest, "https://shenok.dev/problems/validation");
        Assert.True(body.GetProperty("errors").TryGetProperty("PageId", out _));
    }

    private async Task ScanAsync(Guid userId, Guid pageId)
    {
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/scans",
            new
            {
                scanId = Guid.NewGuid().ToString("N"),
                waveformId = Guid.NewGuid(),
                voiceId = Guid.NewGuid(),
                pageId
            },
            userId: userId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<System.Text.Json.JsonElement> ListeningAsync(Guid userId, Guid pageId)
    {
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/listening-state",
            new { pageId, voiceIds = Array.Empty<Guid>() },
            userId: userId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiJson.ReadAsync(response);
    }
}
