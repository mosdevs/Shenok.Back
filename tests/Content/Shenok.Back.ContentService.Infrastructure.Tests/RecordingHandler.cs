using Shenok.Back.ContentService.Domain.DomainEvents;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.ContentService.Infrastructure.Tests;

public sealed class RecordingHandler :
    IDomainEventHandler<WaveformRegisteredDomainEvent>,
    IDomainEventHandler<WaveformRevokedDomainEvent>,
    IDomainEventHandler<PageCreatedDomainEvent>,
    IDomainEventHandler<PagePublishedDomainEvent>,
    IDomainEventHandler<PageArchivedDomainEvent>
{
    private readonly List<IDomainEvent> _events = [];

    public IReadOnlyList<IDomainEvent> Events => _events;
    public bool ThrowOnWaveformRegistered { get; set; }
    public Action<IDomainEvent>? BeforeHandle { get; set; }

    public Task HandleAsync(WaveformRegisteredDomainEvent domainEvent, CancellationToken ct = default) =>
        RecordAsync(domainEvent, ThrowOnWaveformRegistered);

    public Task HandleAsync(WaveformRevokedDomainEvent domainEvent, CancellationToken ct = default) =>
        RecordAsync(domainEvent, throwAfterRecord: false);

    public Task HandleAsync(PageCreatedDomainEvent domainEvent, CancellationToken ct = default) =>
        RecordAsync(domainEvent, throwAfterRecord: false);

    public Task HandleAsync(PagePublishedDomainEvent domainEvent, CancellationToken ct = default) =>
        RecordAsync(domainEvent, throwAfterRecord: false);

    public Task HandleAsync(PageArchivedDomainEvent domainEvent, CancellationToken ct = default) =>
        RecordAsync(domainEvent, throwAfterRecord: false);

    public void Clear() => _events.Clear();

    private Task RecordAsync(IDomainEvent domainEvent, bool throwAfterRecord)
    {
        BeforeHandle?.Invoke(domainEvent);
        _events.Add(domainEvent);
        if (throwAfterRecord)
            throw new InvalidOperationException("Handler failed after the row was committed.");

        return Task.CompletedTask;
    }
}
