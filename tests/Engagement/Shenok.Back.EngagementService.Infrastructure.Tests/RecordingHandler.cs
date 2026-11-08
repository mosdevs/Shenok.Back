using Shenok.Back.EngagementService.Domain.DomainEvents;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.EngagementService.Infrastructure.Tests;

public sealed class RecordingHandler :
    IDomainEventHandler<PageUnlockedDomainEvent>,
    IDomainEventHandler<PageFollowedDomainEvent>,
    IDomainEventHandler<PageUnfollowedDomainEvent>,
    IDomainEventHandler<VoicePlayedDomainEvent>,
    IDomainEventHandler<VoiceCompletedDomainEvent>
{
    private readonly List<IDomainEvent> _events = [];

    public IReadOnlyList<IDomainEvent> Events => _events;
    public bool ThrowOnPageUnlocked { get; set; }
    public Action<IDomainEvent>? BeforeHandle { get; set; }

    public Task HandleAsync(PageUnlockedDomainEvent domainEvent, CancellationToken ct = default) =>
        RecordAsync(domainEvent, ThrowOnPageUnlocked);

    public Task HandleAsync(PageFollowedDomainEvent domainEvent, CancellationToken ct = default) =>
        RecordAsync(domainEvent, throwAfterRecord: false);

    public Task HandleAsync(PageUnfollowedDomainEvent domainEvent, CancellationToken ct = default) =>
        RecordAsync(domainEvent, throwAfterRecord: false);

    public Task HandleAsync(VoicePlayedDomainEvent domainEvent, CancellationToken ct = default) =>
        RecordAsync(domainEvent, throwAfterRecord: false);

    public Task HandleAsync(VoiceCompletedDomainEvent domainEvent, CancellationToken ct = default) =>
        RecordAsync(domainEvent, throwAfterRecord: false);

    public void Clear() => _events.Clear();

    private Task RecordAsync(IDomainEvent domainEvent, bool throwAfterRecord)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        BeforeHandle?.Invoke(domainEvent);
        _events.Add(domainEvent);
        if (throwAfterRecord)
            throw new InvalidOperationException("Handler failed after the row was committed.");

        return Task.CompletedTask;
    }
}
