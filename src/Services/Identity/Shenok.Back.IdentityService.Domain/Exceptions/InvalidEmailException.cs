using Shenok.Back.SharedKernel.Exceptions;

namespace Shenok.Back.IdentityService.Domain.Exceptions
{
    public sealed class InvalidEmailException : DomainException
    {
        public InvalidEmailException(string message) : base(message) { }
    }
}
