using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.IdentityService.Infrastructure.Persistence.DomainEvents
{
    public class DomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public DomainEventDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
        {
            foreach (var domainEvent in domainEvents)
            {
                var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
                var handlers = (IEnumerable<object>)_serviceProvider.GetService(handlerType)!;

                foreach(var handler in handlers)
                {
                    var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
                    await (Task)method.Invoke(handler, new object[] { domainEvent, ct })!;
                }
            }
        }
    }
}
