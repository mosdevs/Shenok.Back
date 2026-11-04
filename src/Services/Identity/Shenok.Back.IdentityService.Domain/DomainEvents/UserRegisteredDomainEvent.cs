using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.IdentityService.Domain.DomainEvents;

public sealed class UserRegisteredDomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccuredOn { get; } = DateTime.UtcNow;
    public UserId UserId { get; }
    public string? Email { get; }
    public string? PhoneNumber { get; }

    public UserRegisteredDomainEvent(UserId userId, string? email, string? phoneNumber)
    {
        UserId = userId;
        Email = email;
        PhoneNumber = phoneNumber;
    }
}
