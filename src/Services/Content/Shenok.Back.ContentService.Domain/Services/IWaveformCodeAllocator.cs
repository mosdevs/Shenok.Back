using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;

namespace Shenok.Back.ContentService.Domain.Services;

/// <summary>
/// Assigns a unique <see cref="WaveformCode"/>.
/// The implementation may use a database sequence or retry when a unique index rejects the code.
/// A revoked code stays reserved.
/// </summary>
public interface IWaveformCodeAllocator
{
    Task<WaveformCode> AllocateAsync(CancellationToken ct = default);
}
