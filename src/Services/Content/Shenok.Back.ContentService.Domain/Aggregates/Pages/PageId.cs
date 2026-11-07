namespace Shenok.Back.ContentService.Domain.Aggregates.Pages;

public sealed record PageId
{
    public Guid Value { get; private set; }

    private PageId()
    {
    }

    private PageId(Guid value) => Value = value;

    public static PageId New() => new(Guid.NewGuid());

    public static PageId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Page id cannot be empty.", nameof(value));

        return new PageId(value);
    }

    public override string ToString() => Value.ToString();
}
