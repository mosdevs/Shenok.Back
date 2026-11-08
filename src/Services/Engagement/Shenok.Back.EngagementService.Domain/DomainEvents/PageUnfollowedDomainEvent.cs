using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.EngagementService.Domain.DomainEvents;

/// <summary>
/// Raised when a follow actually ends. Unlock is left in place.
/// </summary>
public sealed class PageUnfollowedDomainEvent : DomainEvent
{
    public UserId UserId { get; }
    public PageId PageId { get; }

    public PageUnfollowedDomainEvent(UserId userId, PageId pageId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(pageId);
        UserId = userId;
        PageId = pageId;
    }
}
