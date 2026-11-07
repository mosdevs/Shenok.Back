namespace Shenok.Back.ContentService.Domain.Aggregates.Voices;

public sealed record VoiceId
{
    public Guid Value { get; private set; }

    private VoiceId()
    {
    }

    private VoiceId(Guid value) => Value = value;

    public static VoiceId New() => new(Guid.NewGuid());

    public static VoiceId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Voice id cannot be empty.", nameof(value));

        return new VoiceId(value);
    }

    public override string ToString() => Value.ToString();
}
