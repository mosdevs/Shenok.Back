using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Voices.UseCases.AddVoiceToPage;
using Shenok.Back.ContentService.Application.Voices.UseCases.GetVoice;
using Shenok.Back.ContentService.Application.Voices.UseCases.UpdateVoiceTitle;
using Shenok.Back.ContentService.Application.Voices.UseCases.UploadVoice;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Application.Tests;

public class VoiceWorkflowTests
{
    [Fact]
    public async Task Upload_creates_a_pending_voice_under_a_random_key()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var pages = new FakePages();
        await pages.AddAsync(page);
        var voices = new FakeVoices();
        var blobs = new FakeBlobs();
        var unit = new FakeUnitOfWork();
        var useCase = new UploadVoiceUseCase(owner, pages, voices, blobs, unit);
        var content = new MemoryStream(new byte[32]);

        var receipt = await useCase.ExecuteAsync(
            new UploadVoiceRequest(page.Id.Value, "  Episode 1  ", "audio/wav; charset=binary", 32, content),
            CancellationToken.None);

        Assert.Equal(nameof(PackagingStatus.Pending), receipt.PackagingStatus);
        Assert.Equal("Episode 1", receipt.Title);
        Assert.Equal("audio/wav", receipt.ContentType);
        Assert.Equal(32, receipt.SizeBytes);
        Assert.Null(receipt.GetType().GetProperty("SourceStorageKey"));
        var stored = await voices.GetByIdAsync(VoiceId.From(receipt.VoiceId));
        Assert.NotNull(stored);
        Assert.Equal(blobs.Puts.Single(), stored.Audio.SourceStorageKey);
        Assert.StartsWith("sources/", stored.Audio.SourceStorageKey);
        Assert.DoesNotContain("Episode", stored.Audio.SourceStorageKey);
        Assert.Equal(1, unit.Saves);
        Assert.Empty(blobs.Deletes);
        Assert.Equal([stored.Id], page.VoiceIds);
    }

    [Fact]
    public async Task Upload_on_a_published_page_stays_off_the_episode_order()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var ready = ApplicationScenes.Ready(page);
        page.AddVoiceReference(ready.Id, ready.Status);
        page.Publish(new Dictionary<VoiceId, PackagingStatus> { [ready.Id] = ready.Status });
        var pages = new FakePages();
        await pages.AddAsync(page);
        var blobs = new FakeBlobs();
        var useCase = new UploadVoiceUseCase(owner, pages, new FakeVoices(), blobs, new FakeUnitOfWork());

        var receipt = await useCase.ExecuteAsync(
            new UploadVoiceRequest(page.Id.Value, "Next", "audio/mpeg", 8, new MemoryStream(new byte[8])),
            CancellationToken.None);

        Assert.Equal(nameof(PackagingStatus.Pending), receipt.PackagingStatus);
        Assert.Equal([ready.Id], page.VoiceIds);
        Assert.Single(blobs.Puts);
    }

    [Fact]
    public async Task Upload_rejects_a_full_draft_before_writing_a_blob()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        for (var index = 0; index < AudioLimits.MaxVoicesPerPage; index++)
            page.AddVoiceReference(VoiceId.New(), PackagingStatus.Pending);

        var pages = new FakePages();
        await pages.AddAsync(page);
        var blobs = new FakeBlobs();
        var useCase = new UploadVoiceUseCase(owner, pages, new FakeVoices(), blobs, new FakeUnitOfWork());

        var conflict = await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(
                new UploadVoiceRequest(page.Id.Value, "Overflow", "audio/wav", 32, new MemoryStream(new byte[32])),
                CancellationToken.None));

        Assert.Contains(AudioLimits.MaxVoicesPerPage.ToString(), conflict.Message, StringComparison.Ordinal);
        Assert.Empty(blobs.Puts);
        Assert.Equal(AudioLimits.MaxVoicesPerPage, page.VoiceIds.Count);
    }

    [Fact]
    public async Task Upload_then_add_voice_conflicts_because_the_episode_is_already_listed()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var pages = new FakePages();
        await pages.AddAsync(page);
        var voices = new FakeVoices();
        var upload = new UploadVoiceUseCase(owner, pages, voices, new FakeBlobs(), new FakeUnitOfWork());
        var receipt = await upload.ExecuteAsync(
            new UploadVoiceRequest(page.Id.Value, "Episode", "audio/wav", 32, new MemoryStream(new byte[32])),
            CancellationToken.None);

        var add = new AddVoiceToPageUseCase(owner, pages, voices, new FakeUnitOfWork(), new FakeCatalog(voices));
        var conflict = await Assert.ThrowsAsync<ConflictException>(() =>
            add.ExecuteAsync(new AddVoiceToPageRequest(page.Id.Value, receipt.VoiceId), CancellationToken.None));

        Assert.Contains(receipt.VoiceId.ToString(), conflict.Message, StringComparison.Ordinal);
        Assert.Equal([VoiceId.From(receipt.VoiceId)], page.VoiceIds);
    }

    [Fact]
    public async Task Upload_rejects_a_disallowed_type_an_oversized_claim_and_a_short_stream()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var pages = new FakePages();
        await pages.AddAsync(page);
        var blobs = new FakeBlobs();
        var useCase = new UploadVoiceUseCase(owner, pages, new FakeVoices(), blobs, new FakeUnitOfWork());

        await Assert.ThrowsAsync<InvalidAudioAssetException>(() =>
            useCase.ExecuteAsync(new UploadVoiceRequest(page.Id.Value, "Episode", "text/html", 32, new MemoryStream(new byte[32])), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidAudioAssetException>(() =>
            useCase.ExecuteAsync(new UploadVoiceRequest(page.Id.Value, "Episode", "audio/wav", AudioAsset.MaxSourceSizeBytes + 1, new MemoryStream(new byte[1])), CancellationToken.None));

        var shortStream = new MemoryStream(new byte[4]);
        await Assert.ThrowsAsync<InvalidAudioAssetException>(() =>
            useCase.ExecuteAsync(new UploadVoiceRequest(page.Id.Value, "Episode", "audio/wav", 32, shortStream), CancellationToken.None));
        Assert.Empty(blobs.Puts);
    }

    [Fact]
    public async Task Admin_can_upload_to_a_page_they_do_not_own()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var pages = new FakePages();
        await pages.AddAsync(page);
        var admin = new FakeCaller { IsAdmin = true, UserId = Guid.NewGuid() };
        var useCase = new UploadVoiceUseCase(admin, pages, new FakeVoices(), new FakeBlobs(), new FakeUnitOfWork());

        var receipt = await useCase.ExecuteAsync(
            new UploadVoiceRequest(page.Id.Value, "Episode", "audio/mpeg", 8, new MemoryStream(new byte[8])),
            CancellationToken.None);

        Assert.Equal(page.Id.Value, receipt.PageId);
        Assert.Equal(nameof(PackagingStatus.Pending), receipt.PackagingStatus);
        Assert.Equal([VoiceId.From(receipt.VoiceId)], page.VoiceIds);
    }

    [Fact]
    public async Task Add_voice_appends_a_pending_episode_and_rejects_another_pages_voice()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var other = ApplicationScenes.Draft(owner, "Other");
        var voice = ApplicationScenes.Pending(page, "One");
        var foreign = ApplicationScenes.Pending(other, "Foreign");
        var (pages, voices) = await ApplicationScenes.Store(page, voice, foreign);
        await pages.AddAsync(other);
        var unit = new FakeUnitOfWork();
        var useCase = new AddVoiceToPageUseCase(owner, pages, voices, unit, new FakeCatalog(voices));

        var added = await useCase.ExecuteAsync(new AddVoiceToPageRequest(page.Id.Value, voice.Id.Value), CancellationToken.None);
        Assert.Equal([voice.Id.Value], added.Voices.Select(item => item.VoiceId).ToArray());
        Assert.Equal(1, unit.Saves);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new AddVoiceToPageRequest(page.Id.Value, voice.Id.Value), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new AddVoiceToPageRequest(page.Id.Value, foreign.Id.Value), CancellationToken.None));
        Assert.Equal(1, unit.Saves);
    }

    [Fact]
    public async Task Update_title_uses_the_loaded_archive_flag()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var voice = ApplicationScenes.Pending(page);
        var (pages, voices) = await ApplicationScenes.Store(page, voice);
        var unit = new FakeUnitOfWork();
        var useCase = new UpdateVoiceTitleUseCase(owner, voices, pages, unit);

        var renamed = await useCase.ExecuteAsync(new UpdateVoiceTitleRequest(voice.Id.Value, "Renamed"), CancellationToken.None);
        Assert.Equal("Renamed", renamed.Title);
        Assert.Equal(1, unit.Saves);

        page.Archive();
        await Assert.ThrowsAsync<PageArchivedException>(() =>
            useCase.ExecuteAsync(new UpdateVoiceTitleRequest(voice.Id.Value, "Too late"), CancellationToken.None));
        Assert.Equal("Renamed", voice.Title.Value);
        Assert.Equal(1, unit.Saves);

        var stranger = new UpdateVoiceTitleUseCase(new FakeCaller { UserId = Guid.NewGuid() }, voices, pages, unit);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            stranger.ExecuteAsync(new UpdateVoiceTitleRequest(voice.Id.Value, "Stolen"), CancellationToken.None));
    }

    [Fact]
    public async Task Get_voice_returns_packaging_state_without_a_playback_url()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var voice = ApplicationScenes.Ready(page);
        var (pages, voices) = await ApplicationScenes.Store(page, voice);
        var useCase = new GetVoiceUseCase(owner, voices, pages);

        var loaded = await useCase.ExecuteAsync(new GetVoiceRequest(voice.Id.Value), CancellationToken.None);

        Assert.Equal(voice.Id.Value, loaded.VoiceId);
        Assert.Equal(page.Id.Value, loaded.PageId);
        Assert.Equal(9, loaded.DurationSeconds);
        Assert.Equal(nameof(PackagingStatus.Ready), loaded.PackagingStatus);
        Assert.Null(loaded.GetType().GetProperty("PlaylistStorageKey"));
        Assert.Null(loaded.GetType().GetProperty("AudioUrl"));

        var gateway = new GetVoiceUseCase(new FakeCaller { IsInternalService = true, IsContentCreator = false }, voices, pages);
        var internalView = await gateway.ExecuteAsync(new GetVoiceRequest(voice.Id.Value), CancellationToken.None);
        Assert.Equal(loaded.VoiceId, internalView.VoiceId);

        var stranger = new GetVoiceUseCase(new FakeCaller { UserId = Guid.NewGuid() }, voices, pages);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            stranger.ExecuteAsync(new GetVoiceRequest(voice.Id.Value), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new GetVoiceRequest(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Upload_reports_both_failures_when_save_and_delete_fail()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var pages = new FakePages();
        await pages.AddAsync(page);
        var blobs = new FakeBlobs { DeleteFailure = new IOException("blob") };
        var unit = new FakeUnitOfWork { Failure = new InvalidOperationException("database") };
        var useCase = new UploadVoiceUseCase(owner, pages, new FakeVoices(), blobs, unit);

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            useCase.ExecuteAsync(
                new UploadVoiceRequest(page.Id.Value, "Episode", "audio/wav", 32, new MemoryStream(new byte[32])),
                CancellationToken.None));

        Assert.Contains(exception.InnerExceptions, inner => inner is InvalidOperationException);
        Assert.Contains(exception.InnerExceptions, inner => inner is IOException);
        Assert.Empty(blobs.Deletes);
    }
}
