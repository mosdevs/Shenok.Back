using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.IdentityService.Infrastructure.Persistence.DomainEvents;

public sealed class DomainEventDispatcher(IServiceScopeFactory scopeFactory) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        foreach (var domainEvent in domainEvents)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = scope.ServiceProvider.GetServices(handlerType);
            foreach (var handler in handlers)
            {
                var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));
                if (method?.Invoke(handler, [domainEvent, ct]) is Task task)
                    await task;
            }
        }
    }
}
