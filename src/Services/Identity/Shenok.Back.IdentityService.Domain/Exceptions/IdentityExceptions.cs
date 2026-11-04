using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.SharedKernel.Exceptions;

namespace Shenok.Back.IdentityService.Domain.Exceptions;

public sealed class InvalidPhoneNumberException : DomainException
{
    public InvalidPhoneNumberException(string message) : base(message) { }
}

public sealed class InvalidUsernameException : DomainException
{
    public InvalidUsernameException(string message) : base(message) { }
}

public sealed class IdentifierRequiredException : DomainException
{
    public IdentifierRequiredException() : base("At least one of email or phone number is required.") { }
}

public sealed class PasswordRequiredException : DomainException
{
    public PasswordRequiredException() : base("An email account requires a password.") { }
}

public sealed class OtpChallengeFailedException : DomainException
{
    public OtpChallengeFailedException() : base("The verification code is invalid or expired.") { }
}

public sealed class RefreshTokenAlreadyRevokedException : DomainException
{
    public RefreshTokenAlreadyRevokedException() : base("Refresh token is already revoked.") { }
}
