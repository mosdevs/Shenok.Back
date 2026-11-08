using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.DomainEvents;
using Shenok.Back.SharedKernel;

namespace Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;

/// <summary>
/// One client play session. The first accepted progress report counts a play. Later ticks of the same id do not.
/// </summary>
public sealed class PlaySession : AggregateRoot<PlaySessionId>
{
    public UserId UserId { get; private set; } = null!;
    public VoiceId VoiceId { get; private set; } = null!;
    public bool Counted { get; private set; }

    private PlaySession()
    {
    }

    private PlaySession(PlaySessionId id, UserId userId, VoiceId voiceId) : base(id)
    {
        UserId = userId;
        VoiceId = voiceId;
    }

    public static PlaySession Open(PlaySessionId id, UserId userId, VoiceId voiceId)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(voiceId);
        return new PlaySession(id, userId, voiceId);
    }

    /// <summary>
    /// Counts this session once. Returns <see langword="false"/> when it was already counted.
    /// The use case calls this only after a progress report was accepted.
    /// </summary>
    public bool AcceptFirstReport()
    {
        if (Counted)
            return false;

        Counted = true;
        RaiseDomainEvent(new VoicePlayedDomainEvent(Id, UserId, VoiceId));
        return true;
    }
}
