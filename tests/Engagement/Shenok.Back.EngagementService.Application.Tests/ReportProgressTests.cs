using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

namespace Shenok.Back.EngagementService.Application.Tests;

public class ReportProgressTests
{
    [Fact]
    public async Task Ignored_report_does_not_insert_a_row_or_count_a_play()
    {
        var harness = new EngagementHarness();

        await harness.ReportProgress().ExecuteAsync(harness.Progress("play-1", 0), CancellationToken.None);

        Assert.Empty(harness.Memory.ListeningRecords);
        Assert.Empty(harness.Memory.Sessions);
        Assert.Empty(harness.Counters.Plays);
        Assert.Equal(0, harness.Listening.Adds);
    }

    [Fact]
    public async Task Backward_non_seek_does_not_count_a_play_or_rewind()
    {
        var harness = new EngagementHarness();
        var record = ListeningRecord.Start(UserId.From(harness.UserId), VoiceId.From(harness.VoiceId));
        record.ReportProgress(PlaybackPosition.From(50), completed: false, isSeek: false);
        harness.Memory.Seed(record);

        await harness.ReportProgress().ExecuteAsync(harness.Progress("play-1", 10), CancellationToken.None);

        Assert.Equal(50, record.Position.Value);
        Assert.Empty(harness.Memory.Sessions);
        Assert.Empty(harness.Counters.Plays);
    }

    [Fact]
    public async Task First_accepted_report_counts_once_and_a_later_tick_does_not()
    {
        var harness = new EngagementHarness();
        var progress = harness.ReportProgress();

        await progress.ExecuteAsync(harness.Progress("play-1", 10), CancellationToken.None);
        await progress.ExecuteAsync(harness.Progress("play-1", 25), CancellationToken.None);

        Assert.Equal([( "play-1", harness.VoiceId )], harness.Counters.Plays);
        var record = Assert.Single(harness.Memory.ListeningRecords);
        Assert.Equal(25, record.Position.Value);
        Assert.True(Assert.Single(harness.Memory.Sessions).Counted);
    }

    [Fact]
    public async Task Completion_at_position_zero_counts_a_play_without_moving_the_position()
    {
        var harness = new EngagementHarness();

        await harness.ReportProgress().ExecuteAsync(
            harness.Progress("play-1", 0, completed: true),
            CancellationToken.None);

        var record = Assert.Single(harness.Memory.ListeningRecords);
        Assert.Equal(0, record.Position.Value);
        Assert.True(record.IsCompleted);
        Assert.Equal([("play-1", harness.VoiceId)], harness.Counters.Plays);
    }

    [Fact]
    public async Task Seek_backward_is_accepted_and_counts_the_first_play()
    {
        var harness = new EngagementHarness();
        var record = ListeningRecord.Start(UserId.From(harness.UserId), VoiceId.From(harness.VoiceId));
        record.ReportProgress(PlaybackPosition.From(50), completed: false, isSeek: false);
        harness.Memory.Seed(record);

        await harness.ReportProgress().ExecuteAsync(
            harness.Progress("play-1", 10, isSeek: true),
            CancellationToken.None);

        Assert.Equal(10, record.Position.Value);
        Assert.Equal([("play-1", harness.VoiceId)], harness.Counters.Plays);
    }

    [Fact]
    public async Task Play_session_owned_by_another_user_conflicts_and_does_not_commit_listening()
    {
        var harness = new EngagementHarness();
        harness.Memory.Seed(PlaySession.Open(
            PlaySessionId.From("play-1"),
            UserId.New(),
            VoiceId.From(harness.VoiceId)));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            harness.ReportProgress().ExecuteAsync(harness.Progress("play-1", 15), CancellationToken.None));

        Assert.Equal(EngagementConflict.PlaySessionBelongsToAnotherListener, exception.Message);
        Assert.Empty(harness.Counters.Plays);
        Assert.Empty(harness.Memory.ListeningRecords);
        Assert.Equal(0, harness.Listening.Adds);
        Assert.False(Assert.Single(harness.Memory.Sessions).Counted);
    }

    [Fact]
    public async Task Play_session_owned_by_another_voice_conflicts_and_does_not_commit_listening()
    {
        var harness = new EngagementHarness();
        harness.Memory.Seed(PlaySession.Open(
            PlaySessionId.From("play-1"),
            UserId.From(harness.UserId),
            VoiceId.New()));

        await Assert.ThrowsAsync<ConflictException>(() =>
            harness.ReportProgress().ExecuteAsync(harness.Progress("play-1", 15), CancellationToken.None));

        Assert.Empty(harness.Counters.Plays);
        Assert.Empty(harness.Memory.ListeningRecords);
        Assert.False(Assert.Single(harness.Memory.Sessions).Counted);
    }

    [Fact]
    public async Task Lost_play_session_insert_for_another_user_does_not_count_the_play()
    {
        var harness = new EngagementHarness();
        harness.Memory.Failure = new EngagementInsertConflictException(EngagementInsertTarget.PlaySession);
        harness.Memory.FailOnAttempt = 1;
        harness.Memory.BeforeConflict = () => harness.Memory.Seed(PlaySession.Open(
            PlaySessionId.From("play-1"),
            UserId.New(),
            VoiceId.From(harness.VoiceId)));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            harness.ReportProgress().ExecuteAsync(harness.Progress("play-1", 15), CancellationToken.None));

        Assert.Equal(EngagementConflict.PlaySessionBelongsToAnotherListener, exception.Message);
        Assert.Empty(harness.Counters.Plays);
        var record = Assert.Single(harness.Memory.ListeningRecords);
        Assert.Equal(15, record.Position.Value);
        Assert.False(Assert.Single(harness.Memory.Sessions).Counted);
    }

    [Fact]
    public async Task Lost_play_session_insert_for_the_same_listener_does_not_count_again()
    {
        var harness = new EngagementHarness();
        var winner = PlaySession.Open(
            PlaySessionId.From("play-1"),
            UserId.From(harness.UserId),
            VoiceId.From(harness.VoiceId));
        Assert.True(winner.AcceptFirstReport());
        harness.Memory.Failure = new EngagementInsertConflictException(EngagementInsertTarget.PlaySession);
        harness.Memory.FailOnAttempt = 1;
        harness.Memory.BeforeConflict = () => harness.Memory.Seed(winner);

        await harness.ReportProgress().ExecuteAsync(harness.Progress("play-1", 15), CancellationToken.None);

        Assert.Empty(harness.Counters.Plays);
        Assert.Equal(15, Assert.Single(harness.Memory.ListeningRecords).Position.Value);
        Assert.True(Assert.Single(harness.Memory.Sessions).Counted);
    }

    [Fact]
    public async Task Failed_combined_save_retries_and_still_counts_the_play()
    {
        var harness = new EngagementHarness();
        harness.Memory.Failure = new EngagementConcurrencyException();

        await harness.ReportProgress().ExecuteAsync(harness.Progress("play-1", 15), CancellationToken.None);

        Assert.Equal([("play-1", harness.VoiceId)], harness.Counters.Plays);
        Assert.Equal(15, Assert.Single(harness.Memory.ListeningRecords).Position.Value);
        Assert.True(Assert.Single(harness.Memory.Sessions).Counted);
        Assert.Equal(1, harness.Memory.Saves);
    }

    [Fact]
    public async Task Listening_update_reloads_when_it_loses_a_concurrency_race()
    {
        var harness = new EngagementHarness();
        var userId = UserId.From(harness.UserId);
        var voiceId = VoiceId.From(harness.VoiceId);
        var record = ListeningRecord.Start(userId, voiceId);
        record.ReportProgress(PlaybackPosition.From(10), completed: false, isSeek: false);
        harness.Memory.Seed(record);
        harness.Memory.Failure = new EngagementConcurrencyException();
        harness.Memory.BeforeConflict = () =>
        {
            var reloaded = ListeningRecord.Start(userId, voiceId);
            reloaded.ReportProgress(PlaybackPosition.From(10), completed: false, isSeek: false);
            harness.Memory.Seed(reloaded);
        };

        await harness.ReportProgress().ExecuteAsync(harness.Progress("play-1", 30), CancellationToken.None);

        Assert.Equal(30, harness.Memory.FindListening(userId, voiceId)!.Position.Value);
        Assert.Equal([("play-1", harness.VoiceId)], harness.Counters.Plays);
        Assert.Equal(1, harness.Memory.Saves);
    }

    [Fact]
    public async Task Second_persistence_failure_throws()
    {
        var harness = new EngagementHarness();
        harness.Memory.Failure = new EngagementConcurrencyException();
        harness.Memory.BeforeConflict = () => harness.Memory.Failure = new EngagementConcurrencyException();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.ReportProgress().ExecuteAsync(harness.Progress("play-1", 15), CancellationToken.None));

        Assert.Equal("Listening progress could not be saved.", exception.Message);
        Assert.Empty(harness.Counters.Plays);
        Assert.Empty(harness.Memory.ListeningRecords);
        Assert.Empty(harness.Memory.Sessions);
        Assert.Equal(0, harness.Memory.Saves);
    }
}
