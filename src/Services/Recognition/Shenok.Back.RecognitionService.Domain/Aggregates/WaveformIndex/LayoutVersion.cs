using Shenok.Back.RecognitionService.Domain.Exceptions;

namespace Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

/// <summary>
/// Bar layout that produced the code. Version 1 is the only layout the decoder emits.
/// A later version is still a legal index value, so a future layout is not dropped.
/// </summary>
public sealed record LayoutVersion
{
    public const int Version1 = 1;

    public int Value { get; private set; }

    private LayoutVersion()
    {
    }

    private LayoutVersion(int value) => Value = value;

    public static LayoutVersion From(int value)
    {
        if (value < Version1)
            throw new InvalidLayoutVersionException(value);

        return new LayoutVersion(value);
    }

    public override string ToString() => Value.ToString();
}
