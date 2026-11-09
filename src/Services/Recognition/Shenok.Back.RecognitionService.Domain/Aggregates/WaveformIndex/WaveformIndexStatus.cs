namespace Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

/// <summary>
/// Lookup returns a row only while it is <see cref="Active"/>. <see cref="Revoked"/> is a tombstone.
/// </summary>
public enum WaveformIndexStatus
{
    Active = 0,
    Revoked = 1
}
