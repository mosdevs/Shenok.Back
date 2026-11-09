using Microsoft.EntityFrameworkCore;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Infrastructure.Persistence.Repositories;

public sealed class WaveformIndexRepository(RecognitionDbContext db) : IWaveformIndexRepository
{
    public Task<WaveformIndexEntry?> GetActiveAsync(WaveformCode code, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        return db.WaveformIndex
            .AsNoTracking()
            .Where(entry => entry.Status == WaveformIndexStatus.Active)
            .FirstOrDefaultAsync(entry => entry.Code == code, ct);
    }

    public Task<WaveformIndexEntry?> GetAsync(WaveformCode code, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        return db.WaveformIndex
            .AsTracking()
            .FirstOrDefaultAsync(entry => entry.Code == code, ct);
    }

    public async Task AddAsync(WaveformIndexEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await db.WaveformIndex.AddAsync(entry, ct);
    }
}
