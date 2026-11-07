namespace Shenok.Back.SharedKernel.DomainEvents;

public abstract class DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccuredOn { get; } = DateTime.UtcNow;
}
