using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

namespace Shenok.Back.RecognitionService.Domain.Repositories;

/// <summary>
/// Port for the waveform index projection. One row per code.
/// </summary>
public interface IWaveformIndexRepository
{
    /// <summary>
    /// The decode path. Returns null when the code is missing or the row is revoked.
    /// The implementation reads without tracking.
    /// </summary>
    Task<WaveformIndexEntry?> GetActiveAsync(WaveformCode code, CancellationToken ct = default);

    /// <summary>
    /// The apply path. Returns the row in either status, or null when the code has never been seen.
    /// The implementation loads the row for update.
    /// </summary>
    Task<WaveformIndexEntry?> GetAsync(WaveformCode code, CancellationToken ct = default);

    Task AddAsync(WaveformIndexEntry entry, CancellationToken ct = default);
}
