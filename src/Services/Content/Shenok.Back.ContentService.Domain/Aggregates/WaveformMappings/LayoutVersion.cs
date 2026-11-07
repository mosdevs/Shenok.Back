using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;

/// <summary>
/// Bar layout that produced the graphic. Version 1 is 24 bars and 8 heights, matching the waveform codec.
/// </summary>
public sealed record LayoutVersion
{
    public const int Version1 = 1;

    public int Value { get; private set; }

    private LayoutVersion()
    {
    }

    private LayoutVersion(int value) => Value = value;

    public static LayoutVersion V1 { get; } = new(Version1);

    public static LayoutVersion From(int value) => value == Version1
        ? V1
        : throw new InvalidLayoutVersionException(value);

    public override string ToString() => Value.ToString();
}
