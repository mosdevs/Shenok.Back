using Shenok.Back.EngagementService.Domain.DomainEvents;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.DomainEvents;

public sealed class DomainEventDispatcher(IServiceScopeFactory scopeFactory, ILogger<DomainEventDispatcher> logger)
    : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        await using var scope = scopeFactory.CreateAsyncScope();
        foreach (var domainEvent in domainEvents)
            await DispatchOneAsync(scope.ServiceProvider, domainEvent, ct);
    }

    private async Task DispatchOneAsync(IServiceProvider provider, IDomainEvent domainEvent, CancellationToken ct)
    {
        switch (domainEvent)
        {
            case PageUnlockedDomainEvent unlocked:
                await InvokeAsync(provider, unlocked, ct);
                break;
            case PageFollowedDomainEvent followed:
                await InvokeAsync(provider, followed, ct);
                break;
            case PageUnfollowedDomainEvent unfollowed:
                await InvokeAsync(provider, unfollowed, ct);
                break;
            case VoicePlayedDomainEvent played:
                await InvokeAsync(provider, played, ct);
                break;
            case VoiceCompletedDomainEvent completed:
                await InvokeAsync(provider, completed, ct);
                break;
            default:
                logger.LogWarning(
                    "Domain event {EventType} has no in-process handlers.",
                    domainEvent.GetType().Name);
                break;
        }
    }

    private static async Task InvokeAsync<TEvent>(IServiceProvider provider, TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
    {
        foreach (var handler in provider.GetServices<IDomainEventHandler<TEvent>>())
            await handler.HandleAsync(domainEvent, ct);
    }
}
