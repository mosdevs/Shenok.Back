using Shenok.Back.EngagementService.Application.Listening.UseCases.GetListeningState;
using Shenok.Back.EngagementService.Application.Playback.UseCases.CanPlay;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

namespace Shenok.Back.EngagementService.Application.Tests;

public class ReadModelTests
{
    [Fact]
    public async Task CanPlay_is_false_until_the_page_is_unlocked()
    {
        var harness = new EngagementHarness();
        var canPlay = harness.CanPlay();
        var request = new CanPlayRequest(harness.UserId, harness.PageId);

        Assert.False((await canPlay.ExecuteAsync(request, CancellationToken.None)).CanPlay);

        await harness.Follow().ExecuteAsync(harness.FollowRequest(), CancellationToken.None);
        Assert.False((await canPlay.ExecuteAsync(request, CancellationToken.None)).CanPlay);

        await harness.RegisterScan().ExecuteAsync(harness.Scan(), CancellationToken.None);
        Assert.True((await canPlay.ExecuteAsync(request, CancellationToken.None)).CanPlay);
    }

    [Fact]
    public async Task GetListeningState_fills_missing_voices_and_accepts_an_empty_list()
    {
        var harness = new EngagementHarness();
        var otherVoice = Guid.NewGuid();
        var record = ListeningRecord.Start(UserId.From(harness.UserId), VoiceId.From(harness.VoiceId));
        record.ReportProgress(PlaybackPosition.From(12), completed: true, isSeek: false);
        harness.Memory.Seed(record);
        var state = harness.ListeningState();

        var empty = await state.ExecuteAsync(
            new GetListeningStateRequest(harness.UserId, harness.PageId, []),
            CancellationToken.None);
        var filled = await state.ExecuteAsync(
            new GetListeningStateRequest(harness.UserId, harness.PageId, [harness.VoiceId, otherVoice, harness.VoiceId]),
            CancellationToken.None);

        Assert.False(empty.IsFollowing);
        Assert.False(empty.Unlocked);
        Assert.Empty(empty.Voices);
        Assert.False(filled.IsFollowing);
        Assert.False(filled.Unlocked);
        Assert.Equal(
            [
                new ListeningVoiceState(harness.VoiceId, true, 12),
                new ListeningVoiceState(otherVoice, false, 0),
                new ListeningVoiceState(harness.VoiceId, true, 12)
            ],
            filled.Voices);
        Assert.Equal(2, harness.Listening.LastListedCount);

        await harness.RegisterScan().ExecuteAsync(harness.Scan(), CancellationToken.None);
        var afterUnlock = await state.ExecuteAsync(
            new GetListeningStateRequest(harness.UserId, harness.PageId, []),
            CancellationToken.None);
        Assert.False(afterUnlock.IsFollowing);
        Assert.True(afterUnlock.Unlocked);
    }
}
