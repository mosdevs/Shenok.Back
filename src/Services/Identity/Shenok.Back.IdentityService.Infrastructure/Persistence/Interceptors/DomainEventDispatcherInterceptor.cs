using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shenok.Back.SharedKernel.DomainEvents;
using System.Runtime.CompilerServices;

namespace Shenok.Back.IdentityService.Infrastructure.Persistence.Interceptors
{
    public class DomainEventDispatcherInterceptor : SaveChangesInterceptor
    {
        private readonly IDomainEventDispatcher _dispatcher;
        public DomainEventDispatcherInterceptor(IDomainEventDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }
        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context is not null)
                await DispatchDomainEventsAsync(context, cancellationToken);

            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }
        private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken ct)
        {
            var entitiesWithEvents = context.ChangeTracker
                .Entries<IHasDomainEvents>()
                .Select(e => e.Entity)
                .Where(e => e.DomainEvents.Count > 0)
                .ToList();

            foreach (var entity in entitiesWithEvents)
            {
                // Snapshot BEFORE clearing — DomainEvents wraps the same underlying
                // list via AsReadOnly(), so clearing first would empty this reference too.
                var events = entity.DomainEvents.ToList();

                entity.ClearDomainEvents();

                await _dispatcher.DispatchAsync(events, ct);
            }
        }
    }

}

