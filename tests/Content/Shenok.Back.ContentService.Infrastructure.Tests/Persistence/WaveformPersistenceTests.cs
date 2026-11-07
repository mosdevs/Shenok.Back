using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Repositories;
using Shenok.Back.ContentService.Domain.Services;
using Shenok.Back.ContentService.Infrastructure.Persistence;
using Shenok.Back.ContentService.Infrastructure.Persistence.Outbox;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Persistence;

[Collection(InfrastructureFixture.Name)]
public sealed class WaveformPersistenceTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Duplicate_waveform_code_is_rejected_and_the_context_can_save_the_next_code()
    {
        var page = ContentFactory.DraftPage();
        var code = ContentFactory.NextCode();
        var first = ContentFactory.PageMapping(page.Id, code);
        var duplicate = ContentFactory.PageMapping(page.Id, code);
        var duplicateEventId = duplicate.DomainEvents.Single().EventId;

        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var mappings = scope.ServiceProvider.GetRequiredService<IWaveformMappingRepository>();
        await mappings.AddAsync(first);
        await db.SaveChangesAsync();
        fixture.Events.Clear();

        await mappings.AddAsync(duplicate);
        var exception = await Assert.ThrowsAsync<WaveformCodeAlreadyUsedException>(() => db.SaveChangesAsync());

        Assert.DoesNotContain(code.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Added);
        Assert.DoesNotContain(
            fixture.Events.Events,
            domainEvent => domainEvent.EventId == duplicateEventId);

        var retry = ContentFactory.PageMapping(page.Id, ContentFactory.NextCode());
        var retryEventId = retry.DomainEvents.Single().EventId;
        await mappings.AddAsync(retry);
        await db.SaveChangesAsync();

        await using var read = fixture.CreateBareContext();
        Assert.Equal(1, await read.WaveformMappings.CountAsync(mapping => mapping.WaveformCode == WaveformCode.From(code)));
        Assert.Equal(0, await read.OutboxMessages.CountAsync(message => message.Id == duplicateEventId));
        Assert.Equal(1, await read.OutboxMessages.CountAsync(message => message.Id == retryEventId));
    }

    [Fact]
    public async Task Synchronous_save_maps_a_duplicate_waveform_code_the_same_way()
    {
        var page = ContentFactory.DraftPage();
        var code = ContentFactory.NextCode();
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var mappings = scope.ServiceProvider.GetRequiredService<IWaveformMappingRepository>();
        await mappings.AddAsync(ContentFactory.PageMapping(page.Id, code));
        ((DbContext)db).SaveChanges();
        await mappings.AddAsync(ContentFactory.PageMapping(page.Id, code));

        Assert.Throws<WaveformCodeAlreadyUsedException>(() => { ((DbContext)db).SaveChanges(); });
    }

    [Fact]
    public async Task Revoked_code_stays_reserved_and_both_mappings_for_a_target_are_listed()
    {
        var page = ContentFactory.DraftPage();
        var active = ContentFactory.PageMapping(page.Id);
        var revoked = ContentFactory.PageMapping(page.Id);

        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var mappings = scope.ServiceProvider.GetRequiredService<IWaveformMappingRepository>();
            await mappings.AddAsync(active);
            await mappings.AddAsync(revoked);
            await db.SaveChangesAsync();
        }

        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var mappings = scope.ServiceProvider.GetRequiredService<IWaveformMappingRepository>();
            var stored = await mappings.GetByCodeAsync(revoked.WaveformCode);
            Assert.NotNull(stored);
            stored.Revoke();
            await mappings.UpdateAsync(stored);
            await db.SaveChangesAsync();
        }

        await using var readScope = fixture.CreateScope();
        var repository = readScope.ServiceProvider.GetRequiredService<IWaveformMappingRepository>();
        Assert.True(await repository.CodeExistsAsync(revoked.WaveformCode));
        Assert.False(await repository.CodeExistsAsync(WaveformCode.From(ContentFactory.NextCode())));
        var byCode = await repository.GetByCodeAsync(revoked.WaveformCode);
        Assert.NotNull(byCode);
        Assert.Equal(WaveformMappingStatus.Revoked, byCode.Status);
        Assert.False(byCode.IsActive);

        var listed = await repository.ListByTargetAsync(WaveformTargetType.Page, page.Id.Value);
        Assert.Equal(2, listed.Count);
        Assert.Contains(listed, mapping => mapping.Id == active.Id && mapping.IsActive);
        Assert.Contains(listed, mapping => mapping.Id == revoked.Id && !mapping.IsActive);
    }

    [Fact]
    public async Task Register_writes_one_unpublished_outbox_row_in_the_same_save()
    {
        var page = ContentFactory.DraftPage();
        var mapping = ContentFactory.PageMapping(page.Id);
        var domainEvent = mapping.DomainEvents.Single();

        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            db.Add(page);
            await scope.ServiceProvider.GetRequiredService<IWaveformMappingRepository>().AddAsync(mapping);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateBareContext();
        var rows = await read.OutboxMessages.AsNoTracking()
            .Where(message => message.Id == domainEvent.EventId)
            .ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(OutboxMessage.RegisteredType, row.Type);
        Assert.Null(row.PublishedOn);
        Assert.Equal(DateTimeKind.Utc, row.OccurredOn.Kind);

        using var payload = JsonDocument.Parse(row.Payload);
        Assert.Equal(mapping.Id.Value, payload.RootElement.GetProperty("waveformId").GetGuid());
        Assert.Equal(mapping.WaveformCode.Value, payload.RootElement.GetProperty("waveformCode").GetInt64());
        Assert.Equal(LayoutVersion.Version1, payload.RootElement.GetProperty("layoutVersion").GetInt32());
        Assert.Equal("Page", payload.RootElement.GetProperty("targetType").GetString());
        Assert.Equal(page.Id.Value, payload.RootElement.GetProperty("targetId").GetGuid());
        Assert.Equal(domainEvent.EventId, payload.RootElement.GetProperty("eventId").GetGuid());
    }

    [Fact]
    public async Task Allocator_moves_forward_inside_a_transaction_and_does_not_reuse_a_rolled_back_value()
    {
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var allocator = scope.ServiceProvider.GetRequiredService<IWaveformCodeAllocator>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var first = await allocator.AllocateAsync();
        var second = await allocator.AllocateAsync();
        await transaction.RollbackAsync();
        var afterRollback = await allocator.AllocateAsync();

        Assert.InRange(first.Value, 1, WaveformCode.MaxValue);
        Assert.True(second.Value > first.Value);
        Assert.True(afterRollback.Value > second.Value);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => allocator.AllocateAsync(cancelled.Token));
    }
}
