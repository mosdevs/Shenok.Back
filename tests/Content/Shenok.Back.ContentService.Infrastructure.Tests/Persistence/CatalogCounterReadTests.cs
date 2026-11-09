using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Repositories;
using Shenok.Back.ContentService.Infrastructure.Persistence;
using Shenok.Back.ContentService.Infrastructure.Persistence.Counters;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Persistence;

[Collection(InfrastructureFixture.Name)]
public sealed class CatalogCounterReadTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Read_projects_subscriber_and_play_counts_in_one_query()
    {
        await using var scope = fixture.CreateScope();
        var registered = scope.ServiceProvider.GetRequiredService<IPageCatalogReader>();
        Assert.IsType<PostgresPageCatalogReader>(registered);

        var page = ContentFactory.DraftPage();
        var ready = ContentFactory.ReadyVoice(page.Id);
        var pending = ContentFactory.PendingVoice(page.Id);
        var empty = ContentFactory.DraftPage();
        var otherPage = ContentFactory.DraftPage();
        var otherVoice = ContentFactory.PendingVoice(otherPage.Id);
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var pages = scope.ServiceProvider.GetRequiredService<IPageRepository>();
        var voices = scope.ServiceProvider.GetRequiredService<IVoiceRepository>();
        await pages.AddAsync(page);
        await pages.AddAsync(empty);
        await pages.AddAsync(otherPage);
        await voices.AddAsync(ready);
        await voices.AddAsync(pending);
        await voices.AddAsync(otherVoice);
        await db.SaveChangesAsync();

        var ledger = scope.ServiceProvider.GetRequiredService<ICounterLedger>();
        var follow = Guid.NewGuid().ToString("D");
        await ledger.ApplySubscriberDeltaAsync(page.Id.Value, 1, follow);
        await ledger.ApplySubscriberDeltaAsync(page.Id.Value, 1, follow + ":again");
        await ledger.ApplySubscriberDeltaAsync(empty.Id.Value, 1, Guid.NewGuid().ToString("D"));
        for (var play = 0; play < 3; play++)
            await ledger.IncrementPlayAsync(ready.Id.Value, Guid.NewGuid().ToString("D"));
        await ledger.IncrementPlayAsync(otherVoice.Id.Value, Guid.NewGuid().ToString("D"));
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM voice_counters WHERE voice_id = {pending.Id.Value}");

        var interceptor = new ReaderCommandInterceptor();
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql(fixture.PostgresConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using var counted = new ContentDbContext(options);
        var reader = new PostgresPageCatalogReader(counted);

        interceptor.Commands.Clear();
        var catalog = await reader.ReadAsync(page.Id);
        var command = Assert.Single(interceptor.Commands);
        Assert.Contains("page_counters", command, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("voice_counters", command, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("voices", command, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(2, catalog.SubscriberCount);
        var byId = catalog.Voices.ToDictionary(voice => voice.VoiceId);
        Assert.Equal(2, byId.Count);
        Assert.Equal(nameof(PackagingStatus.Ready), byId[ready.Id.Value].PackagingStatus);
        Assert.Equal(12.5, byId[ready.Id.Value].DurationSeconds);
        Assert.Equal("Episode", byId[ready.Id.Value].Title);
        Assert.Equal(3, byId[ready.Id.Value].PlayCount);
        Assert.Equal(nameof(PackagingStatus.Pending), byId[pending.Id.Value].PackagingStatus);
        Assert.Null(byId[pending.Id.Value].DurationSeconds);
        Assert.Equal(0, byId[pending.Id.Value].PlayCount);
        Assert.DoesNotContain(otherVoice.Id.Value, byId.Keys);

        interceptor.Commands.Clear();
        var noVoices = await reader.ReadAsync(empty.Id);
        Assert.Single(interceptor.Commands);
        Assert.Equal(1, noVoices.SubscriberCount);
        Assert.Empty(noVoices.Voices);

        interceptor.Commands.Clear();
        var missing = await reader.ReadAsync(PageId.From(Guid.NewGuid()));
        Assert.Single(interceptor.Commands);
        Assert.Equal(0, missing.SubscriberCount);
        Assert.Empty(missing.Voices);
    }

    private sealed class ReaderCommandInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
