using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.SharedKernel
{
    public abstract class AggregateRoot<TID> : IHasDomainEvents
    {
        // Virtual so a derived aggregate can rebuild a composite id from its own fields.
        // The constructor below assigns Id, so an override setter may only assign fields.
        public virtual TID Id { get; protected set; } = default!;
        private readonly List<IDomainEvent> _domainEvents = new();
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
        protected AggregateRoot() { }
        protected AggregateRoot(TID id)
        {
            Id = id;
        }
        protected void RaiseDomainEvent(IDomainEvent domainEvent)
            => _domainEvents.Add(domainEvent);
        public void ClearDomainEvents()
            => _domainEvents.Clear();
    }
}
