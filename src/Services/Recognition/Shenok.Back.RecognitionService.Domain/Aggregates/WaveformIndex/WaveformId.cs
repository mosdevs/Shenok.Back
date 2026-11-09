namespace Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

/// <summary>
/// Identity of one waveform mapping, copied from the Content event.
/// This is a Recognition type. It is not Content's <c>WaveformId</c>.
/// </summary>
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
