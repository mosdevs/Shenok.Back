using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Domain.Aggregates.Pages;

public sealed record PageTitle
{
    public const int MaxLength = 200;

    public string Value { get; private set; } = string.Empty;

    private PageTitle()
    {
    }

    private PageTitle(string value) => Value = value;

    public static PageTitle Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidPageTitleException("Page title cannot be empty.");

        var trimmed = value.Trim();
        if (trimmed.Length > MaxLength)
            throw new InvalidPageTitleException($"Page title must be {MaxLength} characters or fewer.");

        return new PageTitle(trimmed);
    }

    public override string ToString() => Value;
}
