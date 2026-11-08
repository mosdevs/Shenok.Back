using Shenok.Back.EngagementService.Domain.Exceptions;

namespace Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;

/// <summary>
/// Client-generated play session id. The server does not mint one. Stored length is 1–64 characters after trim.
/// </summary>
public sealed record PlaySessionId
{
    public const int MaxLength = 64;

    public string Value { get; private set; } = string.Empty;

    private PlaySessionId()
    {
    }

    private PlaySessionId(string value) => Value = value;

    public static PlaySessionId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidPlaySessionIdException("Play session id cannot be empty.");

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new InvalidPlaySessionIdException($"Play session id must be {MaxLength} characters or fewer.");

        return new PlaySessionId(trimmed);
    }

    public override string ToString() => Value;
}
