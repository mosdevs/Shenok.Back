using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;

namespace Shenok.Back.ContentService.Domain.Repositories;

public interface IVoiceRepository
{
    Task<Voice?> GetByIdAsync(VoiceId id, CancellationToken ct = default);
    Task<IReadOnlyList<Voice>> ListByPageIdsAsync(IReadOnlyCollection<PageId> pageIds, CancellationToken ct = default);
    Task AddAsync(Voice voice, CancellationToken ct = default);
    Task UpdateAsync(Voice voice, CancellationToken ct = default);
}
