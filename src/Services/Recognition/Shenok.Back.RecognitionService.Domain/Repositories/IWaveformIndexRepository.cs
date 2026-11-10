using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

namespace Shenok.Back.RecognitionService.Domain.Repositories;

/// <summary>
/// Port for the waveform index projection. One row per code.
/// </summary>
public interface IWaveformIndexRepository
{
    /// <summary>
    /// Active-hit read. Returns null when the code is missing or the row is revoked.
    /// The implementation reads without tracking.
    /// </summary>
    Task<WaveformIndexEntry?> GetActiveAsync(WaveformCode code, CancellationToken ct = default);

    /// <summary>
    /// Decode read. Returns the row in either status, or null when the code has never been seen.
    /// The implementation reads without tracking, so a miss does not attach a tombstone.
    /// </summary>
    Task<WaveformIndexEntry?> FindAsync(WaveformCode code, CancellationToken ct = default);

    /// <summary>
    /// The apply path. Returns the row in either status, or null when the code has never been seen.
    /// The implementation loads the row for update. Decode does not call this.
    /// </summary>
    Task<WaveformIndexEntry?> GetAsync(WaveformCode code, CancellationToken ct = default);

    Task AddAsync(WaveformIndexEntry entry, CancellationToken ct = default);
}
