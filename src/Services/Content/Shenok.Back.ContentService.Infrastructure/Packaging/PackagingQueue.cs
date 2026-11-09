using Microsoft.EntityFrameworkCore;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Infrastructure.Persistence;

namespace Shenok.Back.ContentService.Infrastructure.Packaging;

/// <summary>
/// Selects the next pending voices for the local packager.
/// A draft or archived page contributes a voice only when that voice is already in <c>voice_ids</c>.
/// A published page cannot list a pending voice, so a pending voice on a published page stays eligible
/// until it is Ready and the creator attaches it.
/// </summary>
internal static class PackagingQueue
{
    public static async Task<IReadOnlyList<VoiceId>> ListAsync(
        ContentDbContext db,
        int batchSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        var ids = await db.Database.SqlQuery<Guid>(
            $"""
            SELECT v.id AS "Value"
            FROM voices AS v
            INNER JOIN pages AS p ON p.id = v.page_id
            WHERE v.packaging_status = 'Pending'
              AND (
                    p.voice_ids @> to_jsonb(v.id)
                    OR p.status = 'Published'
                  )
            ORDER BY v.id
            LIMIT {batchSize}
            """)
            .ToListAsync(cancellationToken);

        var voices = new VoiceId[ids.Count];
        for (var index = 0; index < ids.Count; index++)
            voices[index] = VoiceId.From(ids[index]);

        return voices;
    }
}
