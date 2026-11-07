using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;

namespace Shenok.Back.ContentService.Domain.Repositories;

public interface IWaveformMappingRepository
{
    Task<WaveformMapping?> GetByIdAsync(WaveformId id, CancellationToken ct = default);
    Task<WaveformMapping?> GetByCodeAsync(WaveformCode code, CancellationToken ct = default);
    Task<IReadOnlyList<WaveformMapping>> ListByTargetAsync(WaveformTargetType targetType, Guid targetId, CancellationToken ct = default);

    /// <summary>
    /// True when any mapping, active or revoked, already uses the code. Codes are not reused.
    /// </summary>
    Task<bool> CodeExistsAsync(WaveformCode code, CancellationToken ct = default);

    Task AddAsync(WaveformMapping mapping, CancellationToken ct = default);
    Task UpdateAsync(WaveformMapping mapping, CancellationToken ct = default);
}
