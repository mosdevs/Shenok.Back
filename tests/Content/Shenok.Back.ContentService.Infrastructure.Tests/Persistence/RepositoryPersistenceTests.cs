using Microsoft.EntityFrameworkCore;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Repositories;
using Shenok.Back.ContentService.Infrastructure.Persistence;
using Shenok.Back.ContentService.Infrastructure.Persistence.Repositories;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Persistence;

public sealed class RepositoryGuardTests
{
    [Fact]
    public async Task Waveform_queries_reject_missing_arguments_before_opening_a_connection()
    {
        await using var db = new ContentDbContext(Options());
        var repository = new WaveformMappingRepository(db);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.GetByIdAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.GetByCodeAsync(null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.ListByTargetAsync((WaveformTargetType)99, Guid.NewGuid()));
        Assert.Empty(await repository.ListByTargetAsync(WaveformTargetType.Page, Guid.Empty));
    }

    [Fact]
    public async Task Page_and_voice_repositories_reject_missing_arguments()
    {
        await using var db = new ContentDbContext(Options());
        var pages = new PageRepository(db);
        var voices = new VoiceRepository(db);

        await Assert.ThrowsAsync<ArgumentNullException>(() => pages.AddAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => pages.GetByIdAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => voices.AddAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => voices.ListByPageIdsAsync(null!));
        Assert.Empty(await voices.ListByPageIdsAsync([]));
    }

    private static DbContextOptions<ContentDbContext> Options() =>
        new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=content;Username=content;Password=content")
            .Options;
}

[Collection(InfrastructureFixture.Name)]
public sealed class RepositoryPersistenceTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Page_reloads_the_episode_order_after_a_reorder()
    {
        var page = ContentFactory.DraftPage();
        var first = VoiceId.New();
        var second = VoiceId.New();
        var third = VoiceId.New();
        page.AddVoiceReference(first, PackagingStatus.Pending);
        page.AddVoiceReference(second, PackagingStatus.Pending);
        page.AddVoiceReference(third, PackagingStatus.Failed);

        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var pages = scope.ServiceProvider.GetRequiredService<IPageRepository>();
            await pages.AddAsync(page);
            await db.SaveChangesAsync();
        }

        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var pages = scope.ServiceProvider.GetRequiredService<IPageRepository>();
            var loaded = await pages.GetByIdAsync(page.Id);
            Assert.NotNull(loaded);
            Assert.Equal([first.Value, second.Value, third.Value], loaded.VoiceIds.Select(id => id.Value));

            loaded.ReorderVoices([third, first, second]);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateBareContext();
        var stored = await read.Pages.AsNoTracking().SingleAsync(item => item.Id == page.Id);
        Assert.Equal([third.Value, first.Value, second.Value], stored.VoiceIds.Select(id => id.Value));
        Assert.Equal(page.Title.Value, stored.Title.Value);
        Assert.Equal(page.OwnerId.Value, stored.OwnerId.Value);
        Assert.Equal(PageStatus.Draft, stored.Status);
    }

    [Fact]
    public async Task List_by_owner_is_ordered_and_does_not_track_edits()
    {
        var owner = OwnerId.New();
        var earlier = ContentFactory.DraftPage(owner);
        var later = ContentFactory.DraftPage(owner);
        var other = ContentFactory.DraftPage();
        Assert.True(earlier.CreatedAt <= later.CreatedAt);

        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var pages = scope.ServiceProvider.GetRequiredService<IPageRepository>();
            await pages.AddAsync(later);
            await pages.AddAsync(other);
            await pages.AddAsync(earlier);
            await db.SaveChangesAsync();
        }

        PageId editedId;
        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var pages = scope.ServiceProvider.GetRequiredService<IPageRepository>();
            var listed = await pages.ListByOwnerAsync(owner);
            Assert.Equal(2, listed.Count);
            Assert.All(listed, page => Assert.Equal(owner, page.OwnerId));
            if (earlier.CreatedAt != later.CreatedAt)
            {
                var firstId = earlier.CreatedAt < later.CreatedAt ? earlier.Id : later.Id;
                Assert.Equal(firstId, listed[0].Id);
            }

            editedId = listed[0].Id;
            listed[0].UpdateMetadata(
                PageTitle.Create("Changed without tracking"),
                PageDescription.Create("This edit must not be stored."),
                coverImageUrl: null);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateBareContext();
        var stored = await read.Pages.AsNoTracking().SingleAsync(page => page.Id == editedId);
        Assert.Equal("Night Walks", stored.Title.Value);
    }

    [Fact]
    public async Task Tracked_get_and_explicit_update_both_persist_a_title()
    {
        var page = ContentFactory.DraftPage();
        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            await scope.ServiceProvider.GetRequiredService<IPageRepository>().AddAsync(page);
            await db.SaveChangesAsync();
        }

        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var loaded = await scope.ServiceProvider.GetRequiredService<IPageRepository>().GetByIdAsync(page.Id);
            Assert.NotNull(loaded);
            loaded.UpdateMetadata(PageTitle.Create("Tracked title"), loaded.Description, loaded.CoverImageUrl);
            await db.SaveChangesAsync();
        }

        page.UpdateMetadata(PageTitle.Create("Detached title"), page.Description, page.CoverImageUrl);
        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            await scope.ServiceProvider.GetRequiredService<IPageRepository>().UpdateAsync(page);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateBareContext();
        var stored = await read.Pages.AsNoTracking().SingleAsync(item => item.Id == page.Id);
        Assert.Equal("Detached title", stored.Title.Value);
        Assert.Null(await read.Pages.AsNoTracking().FirstOrDefaultAsync(item => item.Id == PageId.New()));
    }

    [Fact]
    public async Task Voice_packaging_state_and_page_filter_survive_a_reload()
    {
        var page = ContentFactory.DraftPage();
        var otherPage = ContentFactory.DraftPage();
        var pending = ContentFactory.PendingVoice(page.Id);
        var ready = ContentFactory.ReadyVoice(page.Id);
        var failed = ContentFactory.FailedVoice(page.Id);
        var elsewhere = ContentFactory.PendingVoice(otherPage.Id);

        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            var voices = scope.ServiceProvider.GetRequiredService<IVoiceRepository>();
            await voices.AddAsync(pending);
            await voices.AddAsync(ready);
            await voices.AddAsync(failed);
            await voices.AddAsync(elsewhere);
            await db.SaveChangesAsync();
        }

        await using (var scope = fixture.CreateScope())
        {
            var voices = scope.ServiceProvider.GetRequiredService<IVoiceRepository>();
            var listed = await voices.ListByPageIdsAsync([page.Id, page.Id]);
            Assert.Equal(3, listed.Count);
            Assert.DoesNotContain(listed, voice => voice.Id == elsewhere.Id);
            Assert.Contains(listed, voice => voice.Id == pending.Id && voice.Status == PackagingStatus.Pending && voice.DurationSeconds is null);
            Assert.Contains(listed, voice => voice.Id == ready.Id && voice.Status == PackagingStatus.Ready && voice.DurationSeconds == 12.5);
            Assert.Contains(listed, voice => voice.Id == failed.Id && voice.Status == PackagingStatus.Failed);
        }

        failed.RetryPackagingSuccess(8, $"packages/{Guid.NewGuid():N}/index.m3u8", $"packages/{Guid.NewGuid():N}/");
        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            await scope.ServiceProvider.GetRequiredService<IVoiceRepository>().UpdateAsync(failed);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateBareContext();
        var retried = await read.Voices.AsNoTracking().SingleAsync(voice => voice.Id == failed.Id);
        Assert.Equal(PackagingStatus.Ready, retried.Status);
        Assert.Equal(8, retried.DurationSeconds);
        Assert.Null(await read.Voices.AsNoTracking().FirstOrDefaultAsync(voice => voice.Id == VoiceId.New()));
    }

    [Fact]
    public async Task Two_pending_voices_may_both_omit_playlist_keys()
    {
        var page = ContentFactory.DraftPage();
        var first = ContentFactory.PendingVoice(page.Id);
        var second = ContentFactory.PendingVoice(page.Id);

        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var voices = scope.ServiceProvider.GetRequiredService<IVoiceRepository>();
        await voices.AddAsync(first);
        await voices.AddAsync(second);
        await db.SaveChangesAsync();

        await using var read = fixture.CreateBareContext();
        var stored = await read.Voices.AsNoTracking().Where(voice => voice.Id == first.Id || voice.Id == second.Id).ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.All(stored, voice => Assert.Null(voice.Audio.PlaylistStorageKey));
    }

    [Fact]
    public async Task Duplicate_source_playlist_and_segment_keys_become_a_conflict_without_sql_details()
    {
        var page = ContentFactory.DraftPage();
        var sourceKey = $"sources/{Guid.NewGuid():N}";
        var playlistKey = $"packages/{Guid.NewGuid():N}/index.m3u8";
        var segmentPrefix = $"packages/{Guid.NewGuid():N}/";
        var original = ContentFactory.PendingVoice(page.Id, sourceKey);
        original.CompletePackagingSuccess(4, playlistKey, segmentPrefix);
        var duplicateSource = ContentFactory.PendingVoice(page.Id, sourceKey);
        var duplicatePlaylist = ContentFactory.PendingVoice(page.Id);
        duplicatePlaylist.CompletePackagingSuccess(4, playlistKey, $"packages/{Guid.NewGuid():N}/");
        var duplicatePrefix = ContentFactory.PendingVoice(page.Id);
        duplicatePrefix.CompletePackagingSuccess(4, $"packages/{Guid.NewGuid():N}/index.m3u8", segmentPrefix);

        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            await scope.ServiceProvider.GetRequiredService<IVoiceRepository>().AddAsync(original);
            await db.SaveChangesAsync();
        }

        await AssertConflictAsync(duplicateSource, sourceKey);
        await AssertConflictAsync(duplicatePlaylist, playlistKey);
        await AssertConflictAsync(duplicatePrefix, segmentPrefix);

        await using var read = fixture.CreateBareContext();
        Assert.Equal(1, await read.Voices.CountAsync(voice => voice.Audio.SourceStorageKey == sourceKey));
    }

    private async Task AssertConflictAsync(Voice duplicate, string secret)
    {
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        await scope.ServiceProvider.GetRequiredService<IVoiceRepository>().AddAsync(duplicate);
        var exception = await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesAsync());

        Assert.Equal("A unique value is already in use.", exception.Message);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("23505", exception.ToString(), StringComparison.Ordinal);
    }
}
