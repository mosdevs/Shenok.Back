namespace Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

/// <summary>
/// Identity user id stored in Engagement. Identity remains the source of the user; this type only carries the Guid.
/// </summary>
public sealed record UserId
{
    public Guid Value { get; private set; }

    private UserId()
    {
    }

    private UserId(Guid value) => Value = value;

    public static UserId New() => new(Guid.NewGuid());

    public static UserId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("User id cannot be empty.", nameof(value));

        return new UserId(value);
    }

    public override string ToString() => Value.ToString();
}
