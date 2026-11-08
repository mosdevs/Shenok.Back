using Shenok.Back.EngagementService.Domain.Exceptions;

namespace Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;

/// <summary>
/// Client-generated scan id. The server does not mint one. Stored length is 1–64 characters after trim.
/// </summary>
public sealed record ScanId
{
    public const int MaxLength = 64;

    public string Value { get; private set; } = string.Empty;

    private ScanId()
    {
    }

    private ScanId(string value) => Value = value;

    public static ScanId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidScanIdException("Scan id cannot be empty.");

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new InvalidScanIdException($"Scan id must be {MaxLength} characters or fewer.");

        return new ScanId(trimmed);
    }

    public override string ToString() => Value;
}
