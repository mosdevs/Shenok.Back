using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.EngagementService.Domain.DomainEvents;

/// <summary>
/// Raised once, when a voice becomes completed for a user. Nothing consumes it in this phase.
/// </summary>
public sealed class VoiceCompletedDomainEvent : DomainEvent
{
    public UserId UserId { get; }
    public VoiceId VoiceId { get; }

    public VoiceCompletedDomainEvent(UserId userId, VoiceId voiceId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(voiceId);
        UserId = userId;
        VoiceId = voiceId;
    }
}
