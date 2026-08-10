using Shenok.Back.IdentityService.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace Shenok.Back.IdentityService.Domain.Aggregates.Users
{
    public sealed record Email
    {
        private static readonly Regex EmailPattern =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled); //?
        public string Value { get; }
        private Email(string value) => Value = value; //ctor
        public static Email Create(string value)
        {
            if(string.IsNullOrWhiteSpace(value))
                throw new InvalidEmailException("Email cannot be empty!");
            var normalized = value.Trim().ToLowerInvariant(); //?
            if(!EmailPattern.IsMatch(normalized))
                throw new InvalidEmailException($"{value} is not a valid Email Address!");
            return new Email(normalized);
        }
        public override string ToString() => Value;

    }
}
