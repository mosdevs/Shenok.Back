using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.ContentService.Domain.DomainEvents;

public sealed class PagePublishedDomainEvent : DomainEvent
{
    public PageId PageId { get; }

    public PagePublishedDomainEvent(PageId pageId) => PageId = pageId;
}
