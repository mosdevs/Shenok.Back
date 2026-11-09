using Shenok.Back.ContentService.Domain.Aggregates.Pages;

namespace Shenok.Back.ContentService.Application.Common.Interfaces;

/// <summary>
/// One read of a page's subscriber count and the play count of each voice stored on that page.
/// A missing counter is zero. This read does not change either counter.
/// </summary>
public interface IPageCatalogReader
{
    Task<PageCatalog> ReadAsync(PageId pageId, CancellationToken cancellationToken = default);
}

public sealed record CatalogVoice(
    Guid VoiceId,
    string Title,
    double? DurationSeconds,
    string PackagingStatus,
    long PlayCount);

public sealed record PageCatalog(long SubscriberCount, IReadOnlyList<CatalogVoice> Voices);
