using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.IdentityService.Domain.DomainEvents
{
    public sealed class UserProfileUpdatedDomainEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccuredOn { get; } = DateTime.UtcNow;
        public UserId UserId { get; }
        public UserProfileUpdatedDomainEvent(UserId userId)
        {
            UserId = userId;
        }


    }
}
