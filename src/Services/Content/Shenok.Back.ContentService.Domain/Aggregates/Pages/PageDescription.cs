using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Domain.Aggregates.Pages;

public sealed record PageDescription
{
    public const int MaxLength = 4000;

    public string Value { get; private set; } = string.Empty;

    private PageDescription()
    {
    }

    private PageDescription(string value) => Value = value;

    public static PageDescription Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new PageDescription(string.Empty);

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new InvalidPageDescriptionException($"Page description must be {MaxLength} characters or fewer.");

        return new PageDescription(trimmed);
    }

    public override string ToString() => Value;
}
