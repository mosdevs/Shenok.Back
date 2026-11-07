using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Infrastructure.Persistence;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Packaging;

[Collection(InfrastructureFixture.Name)]
public sealed class VoicePackagingWorkerTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Worker_fails_only_the_pending_voice_when_its_source_is_missing()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await fixture.MigrateAsync(connectionString);
        var pageId = ContentFactory.DraftPage().Id;
        var pending = ContentFactory.PendingVoice(pageId);
        var ready = ContentFactory.ReadyVoice(pageId);
        var failed = ContentFactory.FailedVoice(pageId);
        var readyPlaylist = ready.Audio.PlaylistStorageKey;

        await using (var db = fixture.CreateBareContext(connectionString))
        {
            db.AddRange(pending, ready, failed);
            await db.SaveChangesAsync();
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:content"] = connectionString,
            ["ConnectionStrings:blobs"] = fixture.BlobConnectionString,
            ["Packaging:Enabled"] = "true",
            ["Packaging:PollSeconds"] = "1",
            ["Packaging:BatchSize"] = "8",
            ["Packaging:ProcessTimeoutSeconds"] = "30"
        });
        builder.Services.AddContentInfrastructure(builder.Configuration);
        using var host = builder.Build();
        await host.StartAsync();
        try
        {
            var status = await WaitForStatusAsync(connectionString, pending.Id, PackagingStatus.Failed, TimeSpan.FromSeconds(20));
            Assert.Equal(PackagingStatus.Failed, status);
        }
        finally
        {
            await host.StopAsync();
        }

        await using var read = fixture.CreateBareContext(connectionString);
        var storedReady = await read.Voices.AsNoTracking().SingleAsync(voice => voice.Id == ready.Id);
        var storedFailed = await read.Voices.AsNoTracking().SingleAsync(voice => voice.Id == failed.Id);
        var storedPending = await read.Voices.AsNoTracking().SingleAsync(voice => voice.Id == pending.Id);
        Assert.Equal(PackagingStatus.Ready, storedReady.Status);
        Assert.Equal(readyPlaylist, storedReady.Audio.PlaylistStorageKey);
        Assert.Equal(PackagingStatus.Failed, storedFailed.Status);
        Assert.Equal(PackagingStatus.Failed, storedPending.Status);
        Assert.Null(storedPending.Audio.PlaylistStorageKey);
        Assert.Null(storedPending.DurationSeconds);
    }

    private static async Task<PackagingStatus> WaitForStatusAsync(
        string connectionString,
        VoiceId voiceId,
        PackagingStatus expected,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var current = PackagingStatus.Pending;
        var options = new DbContextOptionsBuilder<ContentDbContext>().UseNpgsql(connectionString).Options;
        while (DateTime.UtcNow < deadline)
        {
            await using var db = new ContentDbContext(options);
            current = await db.Voices.AsNoTracking()
                .Where(voice => voice.Id == voiceId)
                .Select(voice => voice.Audio.PackagingStatus)
                .SingleAsync();
            if (current == expected)
                return current;

            await Task.Delay(200);
        }

        return current;
    }
}
