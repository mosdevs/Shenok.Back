using Microsoft.EntityFrameworkCore;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Repositories;

public sealed class WaveformMappingRepository(ContentDbContext db) : IWaveformMappingRepository
{
    public Task AddAsync(WaveformMapping mapping, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return db.WaveformMappings.AddAsync(mapping, ct).AsTask();
    }

    public Task<bool> CodeExistsAsync(WaveformCode code, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        return db.WaveformMappings.AsNoTracking().AnyAsync(mapping => mapping.WaveformCode == code, ct);
    }

    public Task<WaveformMapping?> GetByCodeAsync(WaveformCode code, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        return db.WaveformMappings.AsTracking().FirstOrDefaultAsync(mapping => mapping.WaveformCode == code, ct);
    }

    public Task<WaveformMapping?> GetByIdAsync(WaveformId id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return db.WaveformMappings.AsTracking().FirstOrDefaultAsync(mapping => mapping.Id == id, ct);
    }

    public async Task<IReadOnlyList<WaveformMapping>> ListByTargetAsync(
        WaveformTargetType targetType,
        Guid targetId,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(targetType))
            throw new ArgumentOutOfRangeException(nameof(targetType), targetType, "Target type is not defined.");
        if (targetId == Guid.Empty)
            return [];

        return await db.WaveformMappings.AsNoTracking()
            .Where(mapping => mapping.TargetType == targetType && mapping.TargetId == targetId)
            .ToListAsync(ct);
    }

    public Task UpdateAsync(WaveformMapping mapping, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        db.WaveformMappings.Update(mapping);
        return Task.CompletedTask;
    }
}
