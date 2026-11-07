using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Domain.Aggregates.Pages;

public sealed record CoverImageUrl
{
    public const int MaxLength = 2048;

    public string Value { get; private set; } = string.Empty;

    private CoverImageUrl()
    {
    }

    private CoverImageUrl(string value) => Value = value;

    public static CoverImageUrl? Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new InvalidCoverImageUrlException($"Cover image URL must be {MaxLength} characters or fewer.");

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidCoverImageUrlException("Cover image URL must be an absolute http or https URL.");

        return new CoverImageUrl(trimmed);
    }

    public override string ToString() => Value;
}
