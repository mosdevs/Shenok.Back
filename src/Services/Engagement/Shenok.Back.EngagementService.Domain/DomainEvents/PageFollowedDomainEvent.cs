using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.EngagementService.Domain.DomainEvents;

/// <summary>
/// Raised when a follow actually starts. The user and page pair is the phase 6 idempotency key.
/// </summary>
public sealed class PageFollowedDomainEvent : DomainEvent
{
    public UserId UserId { get; }
    public PageId PageId { get; }

    public PageFollowedDomainEvent(UserId userId, PageId pageId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(pageId);
        UserId = userId;
        PageId = pageId;
    }
}
