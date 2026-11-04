using System.Text.RegularExpressions;
using Shenok.Back.IdentityService.Domain.Exceptions;

namespace Shenok.Back.IdentityService.Domain.Aggregates.Users;

public sealed record Email
{
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public string Value { get; private set; } = string.Empty;

    private Email()
    {
    }

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidEmailException("Email cannot be empty.");

        var normalized = value.Trim().ToLowerInvariant();
        if (!EmailPattern.IsMatch(normalized))
            throw new InvalidEmailException($"{value} is not a valid email address.");

        return new Email(normalized);
    }

    public override string ToString() => Value;
}
