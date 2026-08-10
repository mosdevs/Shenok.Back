using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.SharedKernel.Exceptions;

namespace Shenok.Back.IdentityService.Domain.Exceptions
{
    public sealed class UserAlreadyVerifiedException : DomainException
    {
        public UserId UserId { get; }
        public UserAlreadyVerifiedException(UserId userId) : base($"User {userId} is already verified!")
        {
            UserId = userId;
        }
    }
}
