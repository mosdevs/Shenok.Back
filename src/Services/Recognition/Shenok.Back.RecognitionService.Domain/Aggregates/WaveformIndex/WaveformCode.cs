using Shenok.Back.RecognitionService.Domain.Exceptions;

namespace Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

/// <summary>
/// Positive integer of at most 40 bits. This is the value the codec reads from the card.
/// The window matches Content's waveform code and layout version 1 of the codec.
/// This is a Recognition type. It is not Content's <c>WaveformCode</c>.
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
