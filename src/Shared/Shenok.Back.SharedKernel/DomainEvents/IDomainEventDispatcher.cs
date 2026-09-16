namespace Shenok.Back.SharedKernel.DomainEvents
{
    public interface IDomainEventDispatcher
    {
        Task DispatchAsync(
            IEnumerable<IDomainEvent> domainEvents,
            CancellationToken ct = default);
    }
}
