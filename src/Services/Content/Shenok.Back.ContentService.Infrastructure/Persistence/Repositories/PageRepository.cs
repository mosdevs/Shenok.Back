using Microsoft.EntityFrameworkCore;
using Shenok.Back.ContentService.Domain.Aggregates.Counters;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Repositories;

public sealed class PageRepository(ContentDbContext db) : IPageRepository
{
    public async Task AddAsync(Page page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        await db.Pages.AddAsync(page, ct);
        await db.PageCounters.AddAsync(PageCounter.Create(page.Id), ct);
    }

    public Task<Page?> GetByIdAsync(PageId id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return db.Pages.AsTracking().FirstOrDefaultAsync(page => page.Id == id, ct);
    }

    public async Task<IReadOnlyList<Page>> ListByOwnerAsync(OwnerId ownerId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        return await db.Pages.AsNoTracking()
            .Where(page => page.OwnerId == ownerId)
            .OrderBy(page => page.CreatedAt)
            .ThenBy(page => page.Id)
            .ToListAsync(ct);
    }

    public Task UpdateAsync(Page page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        db.Pages.Update(page);
        return Task.CompletedTask;
    }
}
