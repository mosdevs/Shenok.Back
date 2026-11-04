using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.IdentityService.Infrastructure.Persistence.Interceptors;

public sealed class DomainEventDispatcherInterceptor(IDomainEventDispatcher dispatcher) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<IHasDomainEvents>> _pending = new();

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

        if (entities.Count > 0)
            _pending.Add(context, entities);
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
}
