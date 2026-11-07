using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shenok.Back.ContentService.Infrastructure.Persistence.Outbox;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Interceptors;

public sealed class DomainEventDispatcherInterceptor(IDomainEventDispatcher dispatcher) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<IHasDomainEvents>> _pending = new();
    private readonly ConditionalWeakTable<DbContext, HashSet<Guid>> _staged = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await DispatchAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DispatchAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ReleaseUncommitted(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ReleaseUncommitted(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
            return;

        _pending.Remove(context);
        var entities = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        StageOutbox(context, entities);
        if (entities.Count > 0)
            _pending.Add(context, entities);
    }

    private void StageOutbox(DbContext context, List<IHasDomainEvents> entities)
    {
        if (!_staged.TryGetValue(context, out var staged))
        {
            staged = [];
            _staged.Add(context, staged);
        }

        foreach (var entity in entities)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                if (!staged.Add(domainEvent.EventId))
                    continue;

                var message = OutboxMessage.From(domainEvent);
                if (message is null)
                {
                    staged.Remove(domainEvent.EventId);
                    continue;
                }

                context.Set<OutboxMessage>().Add(message);
            }
        }
    }

    private async Task DispatchAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || !_pending.TryGetValue(context, out var entities))
            return;

        _pending.Remove(context);
        foreach (var entity in entities)
        {
            var events = entity.DomainEvents.ToList();
            if (events.Count == 0)
                continue;

            await dispatcher.DispatchAsync(events, cancellationToken);
            entity.ClearDomainEvents();
        }
    }

    private void ReleaseUncommitted(DbContext? context)
    {
        if (context is null)
            return;

        _pending.Remove(context);
        if (!_staged.TryGetValue(context, out var staged))
            return;

        foreach (var entry in context.ChangeTracker.Entries<OutboxMessage>())
        {
            if (entry.State == EntityState.Added)
                staged.Remove(entry.Entity.Id);
        }
    }
}
