using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Repositories;
using Shenok.Back.ContentService.Infrastructure.Persistence;
using Shenok.Back.ContentService.Infrastructure.Persistence.Counters;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Persistence;

[Collection(InfrastructureFixture.Name)]
public sealed class CounterLedgerTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Receipts_apply_once_and_a_subscriber_count_does_not_go_below_zero()
    {
        var page = ContentFactory.DraftPage();
        var voice = ContentFactory.PendingVoice(page.Id);
        var otherVoice = ContentFactory.PendingVoice(page.Id);
        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var pages = scope.ServiceProvider.GetRequiredService<IPageRepository>();
            var voices = scope.ServiceProvider.GetRequiredService<IVoiceRepository>();
            await pages.AddAsync(page);
            await voices.AddAsync(voice);
            await voices.AddAsync(otherVoice);
            await db.SaveChangesAsync();
        }

        await using var ledgerScope = fixture.CreateScope();
        var ledger = ledgerScope.ServiceProvider.GetRequiredService<ICounterLedger>();
        var key = $"{Guid.NewGuid():D}:{page.Id.Value:D}";

        var first = await ledger.ApplySubscriberDeltaAsync(page.Id.Value, 1, key);
        var duplicate = await ledger.ApplySubscriberDeltaAsync(page.Id.Value, 1, key);
        var removed = await ledger.ApplySubscriberDeltaAsync(page.Id.Value, -1, key + ":off");
        var floor = await ledger.ApplySubscriberDeltaAsync(page.Id.Value, -1, key + ":floor");
        var missingPageId = Guid.NewGuid();
        var missing = await ledger.ApplySubscriberDeltaAsync(missingPageId, 1, Guid.NewGuid().ToString("D"));
        var playKey = "play-" + Guid.NewGuid().ToString("N");
        var missingPlayKey = "play-" + Guid.NewGuid().ToString("N");
        var played = await ledger.IncrementPlayAsync(voice.Id.Value, playKey);
        var playedAgain = await ledger.IncrementPlayAsync(voice.Id.Value, playKey);
        var missingVoice = await ledger.IncrementPlayAsync(Guid.NewGuid(), missingPlayKey);

        Assert.Equal(new SubscriberCounterUpdate(true, true, 1), first);
        Assert.Equal(new SubscriberCounterUpdate(true, false, 1), duplicate);
        Assert.Equal(new SubscriberCounterUpdate(true, true, 0), removed);
        Assert.Equal(new SubscriberCounterUpdate(true, true, 0), floor);
        Assert.Equal(new SubscriberCounterUpdate(false, false, 0), missing);
        Assert.Equal(new PlayCounterUpdate(true, true, 1), played);
        Assert.Equal(new PlayCounterUpdate(true, false, 1), playedAgain);
        Assert.Equal(new PlayCounterUpdate(false, false, 0), missingVoice);
        await Assert.ThrowsAsync<ConflictException>(() =>
            ledger.ApplySubscriberDeltaAsync(page.Id.Value, -1, key));
        await Assert.ThrowsAsync<ConflictException>(() =>
            ledger.IncrementPlayAsync(otherVoice.Id.Value, playKey));

        await using var read = fixture.CreateBareContext();
        Assert.Equal(0, await read.PageCounters.AsNoTracking().Where(counter => counter.PageId == page.Id).Select(counter => counter.SubscriberCount).SingleAsync());
        Assert.Equal(1, await read.VoiceCounters.AsNoTracking().Where(counter => counter.VoiceId == voice.Id).Select(counter => counter.PlayCount).SingleAsync());
        Assert.Equal(3, await read.Set<SubscriberCounterReceipt>().CountAsync(receipt => receipt.PageId == page.Id.Value));
        Assert.Equal(0, await read.Set<SubscriberCounterReceipt>().CountAsync(receipt => receipt.PageId == missingPageId));
        Assert.Equal(1, await read.Set<PlayCounterReceipt>().CountAsync(receipt => receipt.VoiceId == voice.Id.Value));
        Assert.Equal(0, await read.Set<PlayCounterReceipt>().CountAsync(receipt => receipt.IdempotencyKey == missingPlayKey));
    }
}
