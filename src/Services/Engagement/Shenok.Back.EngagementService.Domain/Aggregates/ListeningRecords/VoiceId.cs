namespace Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;

/// <summary>
/// Content voice id stored in Engagement. Content remains the source of the voice; this type only carries the Guid.
/// </summary>
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
