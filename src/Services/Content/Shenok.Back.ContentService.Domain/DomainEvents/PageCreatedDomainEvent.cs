using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.ContentService.Domain.DomainEvents;

public sealed class PageCreatedDomainEvent : DomainEvent
{
    public PageId PageId { get; }
    public OwnerId OwnerId { get; }

    public PageCreatedDomainEvent(PageId pageId, OwnerId ownerId)
    {
        PageId = pageId;
        OwnerId = ownerId;
    }
}
