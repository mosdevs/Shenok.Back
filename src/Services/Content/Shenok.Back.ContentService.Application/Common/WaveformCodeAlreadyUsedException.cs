namespace Shenok.Back.ContentService.Application.Common;

/// <summary>
/// Raised when a waveform code is already stored.
/// Throw this only when the new mapping was not persisted and is not left tracked as added.
/// The register use case then allocates a different code.
/// </summary>
public sealed class WaveformCodeAlreadyUsedException : Exception
{
    public WaveformCodeAlreadyUsedException()
        : base("Waveform code is already reserved.")
    {
    }
}
