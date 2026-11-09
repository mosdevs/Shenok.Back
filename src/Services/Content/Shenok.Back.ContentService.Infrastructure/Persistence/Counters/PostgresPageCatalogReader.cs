using Microsoft.EntityFrameworkCore;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Counters;

/// <summary>
/// The anchor row keeps a page with no voices in the same SELECT as the subscriber count.
/// Voice rows left-join their play counter, so a missing counter is zero rather than a second query.
/// </summary>
public sealed class PostgresPageCatalogReader(ContentDbContext db) : IPageCatalogReader
{
    public async Task<PageCatalog> ReadAsync(PageId pageId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageId);

        var rows = await db.Database.SqlQuery<PageCatalogRow>($"""
            SELECT
                pc.subscriber_count AS "SubscriberCount",
                v.id AS "VoiceId",
                v.title AS "Title",
                v.duration_seconds AS "DurationSeconds",
                v.packaging_status AS "PackagingStatus",
                vc.play_count AS "PlayCount"
            FROM (SELECT CAST({pageId.Value} AS uuid) AS page_id) AS anchor
            LEFT JOIN page_counters AS pc ON pc.page_id = anchor.page_id
            LEFT JOIN voices AS v ON v.page_id = anchor.page_id
            LEFT JOIN voice_counters AS vc ON vc.voice_id = v.id
            """)
            .ToListAsync(cancellationToken);

        return Map(rows);
    }

    private static PageCatalog Map(List<PageCatalogRow> rows)
    {
        if (rows.Count == 0)
            return new PageCatalog(0, []);

        var voices = new List<CatalogVoice>(rows.Count);
        foreach (var row in rows)
        {
            if (row.VoiceId is not Guid voiceId)
                continue;

            if (row.Title is null || row.PackagingStatus is null)
                throw new InvalidOperationException($"Voice {voiceId:D} is missing its title or packaging status.");

            voices.Add(new CatalogVoice(
                voiceId,
                row.Title,
                row.DurationSeconds,
                row.PackagingStatus,
                row.PlayCount ?? 0));
        }

        return new PageCatalog(rows[0].SubscriberCount ?? 0, voices);
    }
}

internal sealed class PageCatalogRow
{
    public long? SubscriberCount { get; set; }
    public Guid? VoiceId { get; set; }
    public string? Title { get; set; }
    public double? DurationSeconds { get; set; }
    public string? PackagingStatus { get; set; }
    public long? PlayCount { get; set; }
}
