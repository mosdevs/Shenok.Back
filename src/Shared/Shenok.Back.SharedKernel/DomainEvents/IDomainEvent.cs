namespace Shenok.Back.SharedKernel.DomainEvents
{
    public interface IDomainEvent
    {
        Guid EventId { get; }
        DateTime OccuredOn { get; }
    }
}
