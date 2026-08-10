using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.SharedKernel.DomainEvents;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shenok.Back.IdentityService.Domain.DomainEvents
{
    public sealed class UserVerifiedDomainEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();

        public DateTime OccuredOn { get; } = DateTime.UtcNow;
        public UserId UserId { get; }
        public UserVerifiedDomainEvent(UserId userId)
        {
            UserId = userId;
        }
    }
}
