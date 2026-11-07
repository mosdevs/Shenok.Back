namespace Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;

public sealed record WaveformId
{
    public Guid Value { get; private set; }

    private WaveformId()
    {
    }

    private WaveformId(Guid value) => Value = value;

    public static WaveformId New() => new(Guid.NewGuid());

    public static WaveformId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Waveform id cannot be empty.", nameof(value));

        return new WaveformId(value);
    }

    public override string ToString() => Value.ToString();
}
