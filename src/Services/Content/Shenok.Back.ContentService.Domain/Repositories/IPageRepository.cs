using Shenok.Back.ContentService.Domain.Aggregates.Pages;

namespace Shenok.Back.ContentService.Domain.Repositories;

public interface IPageRepository
{
    Task<Page?> GetByIdAsync(PageId id, CancellationToken ct = default);
    Task<IReadOnlyList<Page>> ListByOwnerAsync(OwnerId ownerId, CancellationToken ct = default);
    Task AddAsync(Page page, CancellationToken ct = default);
    Task UpdateAsync(Page page, CancellationToken ct = default);
}
