using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.EngagementService.Domain.DomainEvents;

/// <summary>
/// Raised once, when a page is unlocked for a user. Nothing consumes it in this phase.
/// </summary>
public sealed class PageUnlockedDomainEvent : DomainEvent
{
    public UserId UserId { get; }
    public PageId PageId { get; }

    public PageUnlockedDomainEvent(UserId userId, PageId pageId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(pageId);
        UserId = userId;
        PageId = pageId;
    }
}
