using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.DomainEvents;
using Shenok.Back.SharedKernel;

namespace Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;

/// <summary>
/// One user's playback position for one voice. The first accepted progress report creates the row; scan does not.
/// </summary>
public sealed class ListeningRecord : AggregateRoot<ListeningRecordId>
{
    public UserId UserId { get; private set; } = null!;
    public VoiceId VoiceId { get; private set; } = null!;
    public PlaybackPosition Position { get; private set; } = null!;
    public bool IsCompleted { get; private set; }
    public DateTime? LastPlayedAt { get; private set; }

    private ListeningRecord()
    {
    }

    private ListeningRecord(UserId userId, VoiceId voiceId)
    {
        UserId = userId;
        VoiceId = voiceId;
        Position = PlaybackPosition.Zero;
    }

    public static ListeningRecord Start(UserId userId, VoiceId voiceId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(voiceId);
        return new ListeningRecord(userId, voiceId);
    }

    /// <summary>
    /// Applies one progress report.
    /// A smaller position is stored only when <paramref name="isSeek"/> is true.
    /// <paramref name="completed"/> can set <see cref="IsCompleted"/> and can never clear it.
    /// The report is accepted only when the stored position or completion actually changes.
    /// <see cref="LastPlayedAt"/> moves only on an accepted report.
    /// </summary>
    public ProgressReport ReportProgress(PlaybackPosition position, bool completed, bool isSeek)
    {
        ArgumentNullException.ThrowIfNull(position);

        var positionChanged = false;
        if (isSeek || position.Value >= Position.Value)
        {
            if (position.Value != Position.Value)
            {
                Position = position;
                positionChanged = true;
            }
        }

        var newlyCompleted = false;
        if (completed && !IsCompleted)
        {
            IsCompleted = true;
            newlyCompleted = true;
        }

        if (!positionChanged && !newlyCompleted)
            return new ProgressReport(Accepted: false, NewlyCompleted: false);

        LastPlayedAt = DateTime.UtcNow;
        if (newlyCompleted)
            RaiseDomainEvent(new VoiceCompletedDomainEvent(UserId, VoiceId));

        return new ProgressReport(Accepted: true, NewlyCompleted: newlyCompleted);
    }

    /// <summary>
    /// Rebuilt from <see cref="UserId"/> and <see cref="VoiceId"/>.
    /// Persistence ignores this property and uses those two columns as the primary key.
    /// </summary>
    public override ListeningRecordId Id
    {
        get => new(UserId, VoiceId);
        protected set
        {
            ArgumentNullException.ThrowIfNull(value);
            UserId = value.UserId;
            VoiceId = value.VoiceId;
        }
    }
}
