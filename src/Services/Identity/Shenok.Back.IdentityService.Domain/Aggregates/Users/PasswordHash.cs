namespace Shenok.Back.IdentityService.Domain.Aggregates.Users;

public sealed record PasswordHash
{
    public string Value { get; private set; } = string.Empty;

    private PasswordHash()
    {
    }

    private PasswordHash(string value) => Value = value;

    public static PasswordHash FromHashedValue(string hashedValue)
    {
        if (string.IsNullOrWhiteSpace(hashedValue))
            throw new ArgumentException("Password hash cannot be empty.", nameof(hashedValue));

        return new PasswordHash(hashedValue);
    }

    public override string ToString() => "********";
}
