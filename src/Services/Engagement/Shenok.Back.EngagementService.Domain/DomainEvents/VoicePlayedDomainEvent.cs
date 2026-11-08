using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.EngagementService.Domain.DomainEvents;

/// <summary>
/// Raised once per play session, on the first counted report. <see cref="PlaySessionId"/> is the phase 6 idempotency key.
/// </summary>
public sealed class VoicePlayedDomainEvent : DomainEvent
{
    public PlaySessionId PlaySessionId { get; }
    public UserId UserId { get; }
    public VoiceId VoiceId { get; }

    public VoicePlayedDomainEvent(PlaySessionId playSessionId, UserId userId, VoiceId voiceId)
    {
        ArgumentNullException.ThrowIfNull(playSessionId);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(voiceId);
        PlaySessionId = playSessionId;
        UserId = userId;
        VoiceId = voiceId;
    }
}
