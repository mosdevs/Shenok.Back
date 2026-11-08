using Shenok.Back.EngagementService.Domain.DomainEvents;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.EngagementService.Infrastructure.Tests;

public sealed class RecordingHandler : IDomainEventHandler<PageUnlockedDomainEvent>
{
    private readonly List<IDomainEvent> _events = [];

    public IReadOnlyList<IDomainEvent> Events => _events;

    public Task HandleAsync(PageUnlockedDomainEvent domainEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _events.Add(domainEvent);
        return Task.CompletedTask;
    }

    public void Clear() => _events.Clear();
}
