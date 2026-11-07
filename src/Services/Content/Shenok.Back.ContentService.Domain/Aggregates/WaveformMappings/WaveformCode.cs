using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;

/// <summary>
/// Positive integer of at most 40 bits. This is the value printed in the waveform bars.
/// The limit matches layout version 1 of <c>Shenok.Back.WaveformCodec</c>.
/// </summary>
public sealed record WaveformCode
{
    public const int MaxBits = 40;
    public const long MaxValue = (1L << MaxBits) - 1;

    public long Value { get; private set; }

    private WaveformCode()
    {
    }

    private WaveformCode(long value) => Value = value;

    public static WaveformCode From(long value)
    {
        if (value <= 0 || value > MaxValue)
            throw new InvalidWaveformCodeException(value);

        return new WaveformCode(value);
    }

    public override string ToString() => Value.ToString();
}
