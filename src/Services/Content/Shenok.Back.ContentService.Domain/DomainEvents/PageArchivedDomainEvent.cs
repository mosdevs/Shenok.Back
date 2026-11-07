using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.ContentService.Domain.DomainEvents;

public sealed class PageArchivedDomainEvent : DomainEvent
{
    public PageId PageId { get; }

    public PageArchivedDomainEvent(PageId pageId) => PageId = pageId;
}
