using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.EngagementService.Domain.DomainEvents;

/// <summary>
/// Raised when a follow actually starts. The receipt key is the user, the page, and this event.
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
