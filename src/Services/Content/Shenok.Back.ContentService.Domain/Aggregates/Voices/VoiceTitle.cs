using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Domain.Aggregates.Voices;

public sealed record VoiceTitle
{
    public const int MaxLength = 200;

    public string Value { get; private set; } = string.Empty;

    private VoiceTitle()
    {
    }

    private VoiceTitle(string value) => Value = value;

    public static VoiceTitle Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidVoiceTitleException("Voice title cannot be empty.");

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new InvalidVoiceTitleException($"Voice title must be {MaxLength} characters or fewer.");

        return new VoiceTitle(trimmed);
    }

    public override string ToString() => Value;
}
