using Shenok.Back.ContentService.Domain.DomainEvents;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.DomainEvents;

public sealed class DomainEventDispatcher(IServiceScopeFactory scopeFactory, ILogger<DomainEventDispatcher> logger)
    : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        foreach (var domainEvent in domainEvents)
            await DispatchOneAsync(scope.ServiceProvider, domainEvent, ct);
    }

    private async Task DispatchOneAsync(IServiceProvider provider, IDomainEvent domainEvent, CancellationToken ct)
    {
        switch (domainEvent)
        {
            case WaveformRegisteredDomainEvent registered:
                await InvokeAsync(provider, registered, ct);
                break;
            case WaveformRevokedDomainEvent revoked:
                await InvokeAsync(provider, revoked, ct);
                break;
            case PageCreatedDomainEvent created:
                await InvokeAsync(provider, created, ct);
                break;
            case PagePublishedDomainEvent published:
                await InvokeAsync(provider, published, ct);
                break;
            case PageArchivedDomainEvent archived:
                await InvokeAsync(provider, archived, ct);
                break;
            default:
                logger.LogWarning(
                    "Domain event {EventType} has no in-process handlers.",
                    domainEvent.GetType().Name);
                break;
        }
    }

    private async Task InvokeAsync<TEvent>(IServiceProvider provider, TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
    {
        foreach (var handler in provider.GetServices<IDomainEventHandler<TEvent>>())
        {
            try
            {
                await handler.HandleAsync(domainEvent, ct);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Domain handler {Handler} failed after the row was committed.",
                    handler.GetType().Name);
            }
        }
    }
}
