using Shenok.Back.ContentService.Domain.Aggregates.Pages;

namespace Shenok.Back.ContentService.Domain.Aggregates.Counters;

/// <summary>
/// Projection row keyed by page. SubscriberCount starts at zero.
/// Phase 6 updates it with guarded SQL. Do not increment it from <see cref="Page"/>.
/// </summary>
public sealed class PageCounter
{
    public PageId PageId { get; private set; } = null!;
    public long SubscriberCount { get; private set; }

    private PageCounter()
    {
    }

    private PageCounter(PageId pageId) => PageId = pageId;

    public static PageCounter Create(PageId pageId)
    {
        ArgumentNullException.ThrowIfNull(pageId);
        return new PageCounter(pageId);
    }
}
