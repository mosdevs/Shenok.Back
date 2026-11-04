using System.Text.RegularExpressions;
using Shenok.Back.IdentityService.Domain.Exceptions;

namespace Shenok.Back.IdentityService.Domain.Aggregates.Users;

public sealed record PhoneNumber
{
    private static readonly Regex Digits = new(@"\d+", RegexOptions.Compiled);

    public string Value { get; private set; } = string.Empty;

    private PhoneNumber()
    {
    }

    private PhoneNumber(string value) => Value = value;

    public static PhoneNumber Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidPhoneNumberException("Phone number cannot be empty.");

        var digits = string.Concat(Digits.Matches(value).Select(match => match.Value));
        if (digits.StartsWith("0098", StringComparison.Ordinal))
            digits = digits[4..];
        else if (digits.StartsWith("98", StringComparison.Ordinal) && digits.Length == 12)
            digits = digits[2..];
        else if (digits.StartsWith('0'))
            digits = digits[1..];

        if (!Regex.IsMatch(digits, @"^9\d{9}$"))
            throw new InvalidPhoneNumberException("Phone number must be an Iranian mobile.");

        return new PhoneNumber($"+98{digits}");
    }

    public override string ToString() => Value;
}
