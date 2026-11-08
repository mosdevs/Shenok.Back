using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.DomainEvents;

namespace Shenok.Back.EngagementService.Domain.Tests;

public class ListeningRecordTests
{
    private static readonly DateTime Sentinel = new(2020, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Greater_position_updates_Position_and_LastPlayedAt_and_is_accepted()
    {
        var userId = UserId.New();
        var voiceId = VoiceId.New();
        var record = ListeningRecord.Start(userId, voiceId);

        Assert.Equal(0, record.Position.Value);
        Assert.False(record.IsCompleted);
        Assert.Null(record.LastPlayedAt);
        Assert.Empty(record.DomainEvents);
        Assert.Equal(new ListeningRecordId(userId, voiceId), record.Id);

        var result = record.ReportProgress(PlaybackPosition.From(15), completed: false, isSeek: false);

        Assert.True(result.Accepted);
        Assert.False(result.NewlyCompleted);
        Assert.Equal(15, record.Position.Value);
        Assert.False(record.IsCompleted);
        Assert.NotNull(record.LastPlayedAt);
        Assert.Equal(DateTimeKind.Utc, record.LastPlayedAt.Value.Kind);
        Assert.Empty(record.DomainEvents);
    }

    [Fact]
    public void Smaller_position_without_seek_leaves_Position_and_LastPlayedAt_and_is_not_accepted()
    {
        var record = ListeningRecord.Start(UserId.New(), VoiceId.New());
        Assert.True(record.ReportProgress(PlaybackPosition.From(30), completed: false, isSeek: false).Accepted);
        AggregateAccess.SetProperty(record, nameof(ListeningRecord.LastPlayedAt), Sentinel);

        var result = record.ReportProgress(PlaybackPosition.From(10), completed: false, isSeek: false);

        Assert.False(result.Accepted);
        Assert.False(result.NewlyCompleted);
        Assert.Equal(30, record.Position.Value);
        Assert.Equal(Sentinel, record.LastPlayedAt);
        Assert.Empty(record.DomainEvents);
    }

    [Fact]
    public void Smaller_position_with_seek_stores_the_smaller_position_and_is_accepted()
    {
        var record = ListeningRecord.Start(UserId.New(), VoiceId.New());
        Assert.True(record.ReportProgress(PlaybackPosition.From(30), completed: false, isSeek: false).Accepted);

        var result = record.ReportProgress(PlaybackPosition.From(10), completed: false, isSeek: true);

        Assert.True(result.Accepted);
        Assert.False(result.NewlyCompleted);
        Assert.Equal(10, record.Position.Value);
        Assert.False(record.IsCompleted);
        Assert.NotNull(record.LastPlayedAt);
        Assert.Empty(record.DomainEvents);
    }

    [Fact]
    public void Completed_stays_true_and_VoiceCompletedDomainEvent_is_raised_once()
    {
        var userId = UserId.New();
        var voiceId = VoiceId.New();
        var record = ListeningRecord.Start(userId, voiceId);

        var first = record.ReportProgress(PlaybackPosition.From(10), completed: true, isSeek: false);
        var second = record.ReportProgress(PlaybackPosition.From(12), completed: false, isSeek: false);

        Assert.True(first.Accepted);
        Assert.True(first.NewlyCompleted);
        Assert.True(second.Accepted);
        Assert.False(second.NewlyCompleted);
        Assert.True(record.IsCompleted);
        Assert.Equal(12, record.Position.Value);
        var completed = Assert.Single(record.DomainEvents.OfType<VoiceCompletedDomainEvent>());
        Assert.Equal(userId, completed.UserId);
        Assert.Equal(voiceId, completed.VoiceId);
    }

    [Fact]
    public void Backward_non_seek_with_completed_true_sets_completion_without_rewinding()
    {
        var record = ListeningRecord.Start(UserId.New(), VoiceId.New());
        Assert.True(record.ReportProgress(PlaybackPosition.From(30), completed: false, isSeek: false).Accepted);

        var result = record.ReportProgress(PlaybackPosition.From(10), completed: true, isSeek: false);

        Assert.True(result.Accepted);
        Assert.True(result.NewlyCompleted);
        Assert.Equal(30, record.Position.Value);
        Assert.True(record.IsCompleted);
        Assert.NotNull(record.LastPlayedAt);
        Assert.Single(record.DomainEvents.OfType<VoiceCompletedDomainEvent>());
    }

    [Fact]
    public void Same_position_without_a_completion_change_is_not_accepted()
    {
        var record = ListeningRecord.Start(UserId.New(), VoiceId.New());

        var atZero = record.ReportProgress(PlaybackPosition.From(0), completed: false, isSeek: false);

        Assert.False(atZero.Accepted);
        Assert.False(atZero.NewlyCompleted);
        Assert.Null(record.LastPlayedAt);

        Assert.True(record.ReportProgress(PlaybackPosition.From(20), completed: false, isSeek: false).Accepted);
        AggregateAccess.SetProperty(record, nameof(ListeningRecord.LastPlayedAt), Sentinel);

        var same = record.ReportProgress(PlaybackPosition.From(20), completed: false, isSeek: true);

        Assert.False(same.Accepted);
        Assert.Equal(20, record.Position.Value);
        Assert.Equal(Sentinel, record.LastPlayedAt);
    }

    [Fact]
    public void Completion_at_position_zero_is_accepted_and_does_not_move_position()
    {
        var userId = UserId.New();
        var voiceId = VoiceId.New();
        var record = ListeningRecord.Start(userId, voiceId);

        var result = record.ReportProgress(PlaybackPosition.From(0), completed: true, isSeek: false);

        Assert.True(result.Accepted);
        Assert.True(result.NewlyCompleted);
        Assert.Equal(0, record.Position.Value);
        Assert.True(record.IsCompleted);
        Assert.NotNull(record.LastPlayedAt);
        var completed = Assert.Single(record.DomainEvents.OfType<VoiceCompletedDomainEvent>());
        Assert.Equal(userId, completed.UserId);
        Assert.Equal(voiceId, completed.VoiceId);
    }

    [Fact]
    public void A_later_completed_true_does_not_raise_a_second_event()
    {
        var record = ListeningRecord.Start(UserId.New(), VoiceId.New());
        Assert.True(record.ReportProgress(PlaybackPosition.From(10), completed: true, isSeek: false).NewlyCompleted);

        var again = record.ReportProgress(PlaybackPosition.From(25), completed: true, isSeek: false);

        Assert.True(again.Accepted);
        Assert.False(again.NewlyCompleted);
        Assert.True(record.IsCompleted);
        Assert.Equal(25, record.Position.Value);
        Assert.Single(record.DomainEvents.OfType<VoiceCompletedDomainEvent>());
    }

    [Fact]
    public void Seek_with_completed_false_does_not_clear_IsCompleted()
    {
        var record = ListeningRecord.Start(UserId.New(), VoiceId.New());
        Assert.True(record.ReportProgress(PlaybackPosition.From(40), completed: true, isSeek: false).Accepted);

        var seek = record.ReportProgress(PlaybackPosition.From(5), completed: false, isSeek: true);

        Assert.True(seek.Accepted);
        Assert.False(seek.NewlyCompleted);
        Assert.Equal(5, record.Position.Value);
        Assert.True(record.IsCompleted);
        Assert.Single(record.DomainEvents.OfType<VoiceCompletedDomainEvent>());
    }

    [Fact]
    public void Id_is_rebuilt_from_the_user_and_voice_columns()
    {
        var record = ListeningRecord.Start(UserId.New(), VoiceId.New());
        var userId = UserId.New();
        var voiceId = VoiceId.New();
        AggregateAccess.SetProperty(record, nameof(ListeningRecord.UserId), userId);
        AggregateAccess.SetProperty(record, nameof(ListeningRecord.VoiceId), voiceId);

        Assert.Equal(new ListeningRecordId(userId, voiceId), record.Id);
    }
}
