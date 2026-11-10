using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.RecognitionService.Application.Common;
using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRegistered;
using Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRevoked;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Repositories;
using Shenok.Back.RecognitionService.Infrastructure.Persistence;

namespace Shenok.Back.RecognitionService.Infrastructure.Tests;

[Collection(InfrastructureFixture.Name)]
public sealed class IndexPersistenceTests(InfrastructureFixture fixture)
{
    private static long _nextCode = 50_000;

    [RequiresPostgresFact]
    public async Task Two_adds_of_the_same_code_keep_one_row()
    {
        var code = NextCode();
        await using (var db = fixture.CreateContext())
        {
            db.WaveformIndex.Add(WaveformIndexEntry.Register(Fact(code)));
            await db.SaveChangesAsync();
        }

        await using var duplicate = fixture.CreateContext();
        duplicate.WaveformIndex.Add(WaveformIndexEntry.Register(Fact(code)));
        var exception = await Assert.ThrowsAsync<ConflictException>(() => duplicate.SaveChangesAsync());

        Assert.Equal(RecognitionDbContext.DuplicateSentence, exception.Message);
        Assert.Equal(1, await CountIndexAsync(code));
    }

    [RequiresPostgresFact]
    public async Task Two_adds_of_the_same_event_id_keep_one_inbox_row()
    {
        var eventId = Guid.NewGuid();
        var received = DateTime.UtcNow;
        await using (var db = fixture.CreateContext())
        {
            db.InboxMessages.Add(new InboxMessage(eventId, WaveformMessageType.WaveformRegistered, received));
            await db.SaveChangesAsync();
        }

        await using var duplicate = fixture.CreateContext();
        duplicate.InboxMessages.Add(new InboxMessage(eventId, WaveformMessageType.WaveformRevoked, received));
        await Assert.ThrowsAsync<InboxAlreadyProcessedException>(() => duplicate.SaveChangesAsync());

        Assert.Equal(1, await CountInboxAsync(eventId));
    }

    [RequiresPostgresFact]
    public async Task Register_then_revoke_leaves_no_active_hit_and_one_row()
    {
        var code = NextCode();
        var waveformId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var registeredAt = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

        await using (var scope = fixture.CreateScope())
        {
            var register = scope.ServiceProvider.GetRequiredService<ApplyWaveformRegisteredUseCase>();
            var revoke = scope.ServiceProvider.GetRequiredService<ApplyWaveformRevokedUseCase>();
            await register.ExecuteAsync(
                Request<ApplyWaveformRegisteredRequest>(Guid.NewGuid(), registeredAt, waveformId, code, targetId),
                CancellationToken.None);
            await revoke.ExecuteAsync(
                Request<ApplyWaveformRevokedRequest>(Guid.NewGuid(), registeredAt.AddMinutes(1), waveformId, code, targetId),
                CancellationToken.None);
        }

        await using var read = fixture.CreateScope();
        var index = read.ServiceProvider.GetRequiredService<IWaveformIndexRepository>();
        Assert.Null(await index.GetActiveAsync(WaveformCode.From(code)));
        Assert.Equal(1, await CountIndexAsync(code));
    }

    [RequiresPostgresFact]
    public async Task Replaying_a_registered_event_does_not_insert_a_second_row()
    {
        var code = NextCode();
        var eventId = Guid.NewGuid();
        var waveformId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var occurredOn = new DateTime(2026, 11, 9, 12, 5, 0, DateTimeKind.Utc);
        var request = Request<ApplyWaveformRegisteredRequest>(eventId, occurredOn, waveformId, code, targetId);

        await using (var scope = fixture.CreateScope())
        {
            var register = scope.ServiceProvider.GetRequiredService<ApplyWaveformRegisteredUseCase>();
            await register.ExecuteAsync(request, CancellationToken.None);
            await register.ExecuteAsync(request, CancellationToken.None);
        }

        Assert.Equal(1, await CountInboxAsync(eventId));
        Assert.Equal(1, await CountIndexAsync(code));
    }

    [RequiresPostgresFact]
    public async Task FindAsync_reads_a_revoked_row_and_does_not_track_it()
    {
        var code = NextCode();
        await using (var seed = fixture.CreateContext())
        {
            seed.WaveformIndex.Add(WaveformIndexEntry.RevokeFirst(Fact(code)));
            await seed.SaveChangesAsync();
        }

        await using var scope = fixture.CreateScope();
        var index = scope.ServiceProvider.GetRequiredService<IWaveformIndexRepository>();
        var db = scope.ServiceProvider.GetRequiredService<RecognitionDbContext>();
        var storedCode = WaveformCode.From(code);

        var snapshot = await index.FindAsync(storedCode);
        Assert.NotNull(snapshot);
        Assert.Equal(WaveformIndexStatus.Revoked, snapshot.Status);
        Assert.False(snapshot.IsActive);
        Assert.Empty(db.ChangeTracker.Entries());

        Assert.Null(await index.GetActiveAsync(storedCode));
        Assert.Empty(db.ChangeTracker.Entries());

        var tracked = await index.GetAsync(storedCode);
        Assert.NotNull(tracked);
        Assert.Equal(EntityState.Unchanged, db.Entry(tracked).State);
    }

    [RequiresPostgresFact]
    public async Task A_lost_xmin_update_throws_and_keeps_the_first_write()
    {
        var code = NextCode();
        var waveformId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var registeredAt = new DateTime(2026, 11, 9, 12, 10, 0, DateTimeKind.Utc);
        await using (var seed = fixture.CreateContext())
        {
            seed.WaveformIndex.Add(WaveformIndexEntry.Register(Fact(code, registeredAt, waveformId, targetId)));
            await seed.SaveChangesAsync();
        }

        await using var winner = fixture.CreateContext();
        await using var loser = fixture.CreateContext();
        var storedCode = WaveformCode.From(code);
        var first = await winner.WaveformIndex.AsTracking().SingleAsync(entry => entry.Code == storedCode);
        var second = await loser.WaveformIndex.AsTracking().SingleAsync(entry => entry.Code == storedCode);
        first.ApplyRevoked(Fact(code, registeredAt.AddMinutes(1), waveformId, targetId));
        second.ApplyRevoked(Fact(code, registeredAt.AddMinutes(2), waveformId, targetId));

        await winner.SaveChangesAsync();
        await Assert.ThrowsAsync<IndexConcurrencyException>(() => loser.SaveChangesAsync());

        await using var read = fixture.CreateContext();
        var committed = await read.WaveformIndex.AsNoTracking().SingleAsync(entry => entry.Code == storedCode);
        Assert.Equal(WaveformIndexStatus.Revoked, committed.Status);
        Assert.Equal(registeredAt.AddMinutes(1), committed.SourceOccurredOn);
        Assert.Equal(1, await CountIndexAsync(code));
    }

    private static long NextCode() => Interlocked.Increment(ref _nextCode);

    private static WaveformFact Fact(long code, DateTime? occurredOn = null, Guid? waveformId = null, Guid? targetId = null)
        => WaveformFact.Create(
            WaveformCode.From(code),
            WaveformId.From(waveformId ?? Guid.NewGuid()),
            WaveformTargetType.Page,
            targetId ?? Guid.NewGuid(),
            LayoutVersion.Version1,
            occurredOn ?? DateTime.UtcNow,
            Guid.NewGuid());

    private static TRequest Request<TRequest>(Guid eventId, DateTime occurredOn, Guid waveformId, long code, Guid targetId)
        where TRequest : class
    {
        return (TRequest)Activator.CreateInstance(
            typeof(TRequest),
            eventId,
            occurredOn,
            waveformId,
            code,
            LayoutVersion.Version1,
            nameof(WaveformTargetType.Page),
            targetId)!;
    }

    private async Task<int> CountIndexAsync(long code)
    {
        await using var db = fixture.CreateContext();
        var codes = await db.WaveformIndex.AsNoTracking().Select(entry => entry.Code).ToListAsync();
        return codes.Count(item => item.Value == code);
    }

    private async Task<int> CountInboxAsync(Guid eventId)
    {
        await using var db = fixture.CreateContext();
        return await db.InboxMessages.CountAsync(message => message.EventId == eventId);
    }
}
