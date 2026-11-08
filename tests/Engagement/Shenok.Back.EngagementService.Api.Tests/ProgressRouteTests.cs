using System.Net;
using System.Text.Json;
using Shenok.Back.EngagementService.Application.Common;

namespace Shenok.Back.EngagementService.Api.Tests;

[Collection("Engagement API")]
public sealed class ProgressRouteTests(EngagementApiFixture api)
{
    [Fact]
    public async Task Negative_position_is_validation_400()
    {
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/progress",
            new
            {
                voiceId = Guid.NewGuid(),
                playSessionId = Guid.NewGuid().ToString("N"),
                positionSeconds = -1,
                completed = false,
                isSeek = false
            },
            userId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(body, HttpStatusCode.BadRequest, "https://shenok.dev/problems/validation");
        Assert.True(body.GetProperty("errors").TryGetProperty("PositionSeconds", out _));
        Assert.NotEqual("https://shenok.dev/problems/server", body.GetProperty("type").GetString());
    }

    [Fact]
    public async Task A_smaller_position_does_not_rewind_unless_the_report_is_a_seek()
    {
        var userId = Guid.NewGuid();
        var voiceId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString("N");

        await ReportAsync(userId, voiceId, sessionId, position: 40, isSeek: false);
        await ReportAsync(userId, voiceId, sessionId, position: 10, isSeek: false);
        var held = await PositionAsync(userId, voiceId);
        Assert.Equal(40, held);

        await ReportAsync(userId, voiceId, sessionId, position: 5, isSeek: true);
        var sought = await PositionAsync(userId, voiceId);
        Assert.Equal(5, sought);
    }

    [Fact]
    public async Task Completion_stays_true()
    {
        var userId = Guid.NewGuid();
        var voiceId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString("N");

        await ReportAsync(userId, voiceId, sessionId, position: 1, completed: true);
        await ReportAsync(userId, voiceId, sessionId, position: 2, completed: false);

        var state = await StateAsync(userId, voiceId);
        var voice = state.GetProperty("voices").EnumerateArray().Single();
        Assert.True(voice.GetProperty("isCompleted").GetBoolean());
        Assert.Equal(2, voice.GetProperty("positionSeconds").GetInt32());
    }

    [Fact]
    public async Task Play_session_owned_by_another_user_is_409_and_does_not_store_the_new_row()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var voiceId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString("N");
        await ReportAsync(owner, voiceId, sessionId, position: 8);

        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/progress",
            Progress(voiceId, sessionId, position: 15),
            userId: other);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ApiJson.ReadAsync(response);
        ApiJson.AssertProblem(problem, HttpStatusCode.Conflict, "https://shenok.dev/problems/conflict");
        Assert.Equal(EngagementConflict.PlaySessionBelongsToAnotherListener, problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("positionSeconds", out _));

        var otherState = await StateAsync(other, voiceId);
        var voice = otherState.GetProperty("voices").EnumerateArray().Single();
        Assert.Equal(0, voice.GetProperty("positionSeconds").GetInt32());
        Assert.False(voice.GetProperty("isCompleted").GetBoolean());

        Assert.Equal(8, await PositionAsync(owner, voiceId));
    }

    private async Task ReportAsync(
        Guid userId,
        Guid voiceId,
        string sessionId,
        int position,
        bool completed = false,
        bool isSeek = false)
    {
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/progress",
            Progress(voiceId, sessionId, position, completed, isSeek),
            userId: userId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<int> PositionAsync(Guid userId, Guid voiceId)
    {
        var voice = (await StateAsync(userId, voiceId)).GetProperty("voices").EnumerateArray().Single();
        return voice.GetProperty("positionSeconds").GetInt32();
    }

    private async Task<JsonElement> StateAsync(Guid userId, Guid voiceId)
    {
        var response = await api.SendAsync(
            HttpMethod.Post,
            "/internal/listening-state",
            new { pageId = Guid.NewGuid(), voiceIds = new[] { voiceId } },
            userId: userId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ApiJson.ReadAsync(response);
    }

    private static object Progress(
        Guid voiceId,
        string sessionId,
        int position,
        bool completed = false,
        bool isSeek = false) => new
    {
        voiceId,
        playSessionId = sessionId,
        positionSeconds = position,
        completed,
        isSeek
    };
}
