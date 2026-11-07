namespace Shenok.Back.ContentService.Domain.Aggregates.Pages;

/// <summary>
/// Identity user id stored in Content. Identity remains the source of the user; this type only carries the Guid.
/// </summary>
public sealed record OwnerId
{
    public Guid Value { get; private set; }

    private OwnerId()
    {
    }

    private OwnerId(Guid value) => Value = value;

    public static OwnerId New() => new(Guid.NewGuid());

    public static OwnerId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Owner id cannot be empty.", nameof(value));

        return new OwnerId(value);
    }

    public override string ToString() => Value.ToString();
}
