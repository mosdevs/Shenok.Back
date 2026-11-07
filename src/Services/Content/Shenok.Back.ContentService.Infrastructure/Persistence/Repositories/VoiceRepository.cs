using Microsoft.EntityFrameworkCore;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Repositories;

public sealed class VoiceRepository(ContentDbContext db) : IVoiceRepository
{
    public Task AddAsync(Voice voice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(voice);
        return db.Voices.AddAsync(voice, ct).AsTask();
    }

    public Task<Voice?> GetByIdAsync(VoiceId id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return db.Voices.AsTracking().FirstOrDefaultAsync(voice => voice.Id == id, ct);
    }

    public async Task<IReadOnlyList<Voice>> ListByPageIdsAsync(IReadOnlyCollection<PageId> pageIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pageIds);
        if (pageIds.Count == 0)
            return [];

        var ids = pageIds.Distinct().ToArray();
        return await db.Voices.AsNoTracking()
            .Where(voice => ids.Contains(voice.PageId))
            .ToListAsync(ct);
    }

    public Task UpdateAsync(Voice voice, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(voice);
        db.Voices.Update(voice);
        return Task.CompletedTask;
    }
}
