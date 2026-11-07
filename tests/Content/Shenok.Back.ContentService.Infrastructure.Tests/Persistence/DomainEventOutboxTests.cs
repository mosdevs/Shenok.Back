using Microsoft.EntityFrameworkCore;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.DomainEvents;
using Shenok.Back.ContentService.Infrastructure.Persistence;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Persistence;

[Collection(InfrastructureFixture.Name)]
public sealed class DomainEventOutboxTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Page_events_are_dispatched_after_commit_and_do_not_write_the_outbox()
    {
        var page = ContentFactory.DraftPage();
        var voiceId = VoiceId.New();
        page.AddVoiceReference(voiceId, PackagingStatus.Ready);
        fixture.Events.Clear();

        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            db.Add(page);
            await db.SaveChangesAsync();
            page.Publish(new Dictionary<VoiceId, PackagingStatus> { [voiceId] = PackagingStatus.Ready });
            await db.SaveChangesAsync();
            page.Archive();
            await db.SaveChangesAsync();
        }

        Assert.Contains(fixture.Events.Events, domainEvent => domainEvent is PageCreatedDomainEvent);
        Assert.Contains(fixture.Events.Events, domainEvent => domainEvent is PagePublishedDomainEvent);
        Assert.Contains(fixture.Events.Events, domainEvent => domainEvent is PageArchivedDomainEvent);
        await using var read = fixture.CreateBareContext();
        var payloads = await read.OutboxMessages.AsNoTracking().Select(message => message.Payload).ToListAsync();
        Assert.DoesNotContain(payloads, payload => payload.Contains(page.Id.Value.ToString(), StringComparison.OrdinalIgnoreCase));
        var stored = await read.Pages.AsNoTracking().SingleAsync(item => item.Id == page.Id);
        Assert.Equal(PageStatus.Archived, stored.Status);
    }

    [Fact]
    public async Task Handler_runs_only_after_the_mapping_is_visible_to_another_connection()
    {
        var page = ContentFactory.DraftPage();
        var mapping = ContentFactory.PageMapping(page.Id);
        var seenCommitted = false;
        fixture.Events.Clear();
        fixture.Events.BeforeHandle = domainEvent =>
        {
            if (domainEvent is not WaveformRegisteredDomainEvent registered || registered.WaveformId != mapping.Id)
                return;

            using var other = fixture.CreateBareContext();
            seenCommitted = other.WaveformMappings.AsNoTracking().Any(item => item.Id == mapping.Id);
        };

        try
        {
            await using var scope = fixture.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            db.Add(mapping);
            await db.SaveChangesAsync();
        }
        finally
        {
            fixture.Events.BeforeHandle = null;
        }

        Assert.True(seenCommitted);
        Assert.Contains(fixture.Events.Events, domainEvent => domainEvent is WaveformRegisteredDomainEvent registered && registered.WaveformId == mapping.Id);
    }

    [Fact]
    public async Task Handler_failure_does_not_roll_back_the_committed_mapping()
    {
        var page = ContentFactory.DraftPage();
        var mapping = ContentFactory.PageMapping(page.Id);
        fixture.Events.Clear();
        fixture.Events.ThrowOnWaveformRegistered = true;

        try
        {
            await using var scope = fixture.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            db.Add(mapping);
            var exception = await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
            var root = exception;
            while (root is System.Reflection.TargetInvocationException { InnerException: not null } wrapped)
                root = wrapped.InnerException;
            Assert.Equal("Handler failed after the row was committed.", root!.Message);
        }
        finally
        {
            fixture.Events.ThrowOnWaveformRegistered = false;
        }

        await using var read = fixture.CreateBareContext();
        Assert.True(await read.WaveformMappings.AsNoTracking().AnyAsync(item => item.Id == mapping.Id));
        Assert.True(await read.OutboxMessages.AsNoTracking().AnyAsync(message => message.Id == mapping.DomainEvents.Single().EventId));
    }

    [Fact]
    public async Task Failed_waveform_save_does_not_dispatch_or_leave_an_outbox_row()
    {
        var page = ContentFactory.DraftPage();
        var code = ContentFactory.NextCode();
        var first = ContentFactory.PageMapping(page.Id, code);
        var duplicate = ContentFactory.PageMapping(page.Id, code);
        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            db.Add(first);
            await db.SaveChangesAsync();
        }

        fixture.Events.Clear();
        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            db.Add(duplicate);
            await Assert.ThrowsAsync<WaveformCodeAlreadyUsedException>(() => db.SaveChangesAsync());
        }

        Assert.DoesNotContain(fixture.Events.Events, domainEvent => domainEvent.EventId == duplicate.DomainEvents.Single().EventId);
        await using var read = fixture.CreateBareContext();
        Assert.False(await read.WaveformMappings.AnyAsync(mapping => mapping.Id == duplicate.Id));
        Assert.False(await read.OutboxMessages.AnyAsync(message => message.Id == duplicate.DomainEvents.Single().EventId));
    }
}
