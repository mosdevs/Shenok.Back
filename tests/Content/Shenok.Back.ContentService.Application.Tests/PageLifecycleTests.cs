using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Pages.UseCases.ArchivePage;
using Shenok.Back.ContentService.Application.Pages.UseCases.CreatePage;
using Shenok.Back.ContentService.Application.Pages.UseCases.GetPage;
using Shenok.Back.ContentService.Application.Pages.UseCases.PublishPage;
using Shenok.Back.ContentService.Application.Pages.UseCases.ReorderVoices;
using Shenok.Back.ContentService.Application.Pages.UseCases.UpdatePageMetadata;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Application.Tests;

public class PageLifecycleTests
{
    [Fact]
    public async Task Admin_can_create_a_page_without_the_creator_role()
    {
        var caller = new FakeCaller { IsAdmin = true, IsContentCreator = false };
        var unit = new FakeUnitOfWork();
        var useCase = new CreatePageUseCase(caller, new FakePages(), unit, new FakeCatalog(new FakeVoices()));

        var created = await useCase.ExecuteAsync(new CreatePageRequest("Night Walks", null, "https://cdn.example/cover.jpg"), CancellationToken.None);

        Assert.Equal(caller.UserId, created.OwnerId);
        Assert.Equal("https://cdn.example/cover.jpg", created.CoverImageUrl);
        Assert.Equal(1, unit.Saves);
    }

    [Fact]
    public async Task Create_rejects_a_missing_caller_id()
    {
        var caller = new FakeCaller { UserId = Guid.Empty };
        var unit = new FakeUnitOfWork();
        var useCase = new CreatePageUseCase(caller, new FakePages(), unit, new FakeCatalog(new FakeVoices()));

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            useCase.ExecuteAsync(new CreatePageRequest("Night Walks", null, null), CancellationToken.None));
        Assert.Equal(0, unit.Saves);
    }

    [Fact]
    public async Task Get_page_returns_voices_in_episode_order_for_the_owner_and_an_internal_caller()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var first = ApplicationScenes.Pending(page, "First");
        var second = ApplicationScenes.Pending(page, "Second");
        page.AddVoiceReference(first.Id, first.Status);
        page.AddVoiceReference(second.Id, second.Status);
        var (pages, voices) = await ApplicationScenes.Store(page, first, second);
        var catalog = new FakeCatalog(voices);
        catalog.SetSubscriberCount(page.Id.Value, 4);
        catalog.SetPlayCount(first.Id.Value, 9);
        var useCase = new GetPageUseCase(owner, pages, catalog);

        var loaded = await useCase.ExecuteAsync(new GetPageRequest(page.Id.Value), CancellationToken.None);

        Assert.Equal([first.Id.Value, second.Id.Value], loaded.Voices.Select(voice => voice.VoiceId).ToArray());
        Assert.Equal("First", loaded.Voices[0].Title);
        Assert.Equal(4, loaded.SubscriberCount);
        Assert.Equal(9, loaded.Voices[0].PlayCount);
        Assert.Equal(0, loaded.Voices[1].PlayCount);
        Assert.All(loaded.Voices, voice => Assert.Equal(nameof(PackagingStatus.Pending), voice.PackagingStatus));

        var gateway = new GetPageUseCase(new FakeCaller { IsInternalService = true, IsContentCreator = false }, pages, catalog);
        var internalView = await gateway.ExecuteAsync(new GetPageRequest(page.Id.Value), CancellationToken.None);
        Assert.Equal(loaded.Voices.Select(voice => voice.VoiceId), internalView.Voices.Select(voice => voice.VoiceId));
    }

    [Fact]
    public async Task Get_page_hides_another_creators_page_and_a_missing_id()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var (pages, voices) = await ApplicationScenes.Store(page);
        var stranger = new GetPageUseCase(new FakeCaller { UserId = Guid.NewGuid() }, pages, new FakeCatalog(voices));

        var denied = await Assert.ThrowsAsync<ForbiddenException>(() =>
            stranger.ExecuteAsync(new GetPageRequest(page.Id.Value), CancellationToken.None));
        Assert.Equal("You cannot view this page.", denied.Message);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            stranger.ExecuteAsync(new GetPageRequest(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            stranger.ExecuteAsync(new GetPageRequest(Guid.Empty), CancellationToken.None));
    }

    [Fact]
    public async Task Get_page_fails_when_an_episode_row_is_missing()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var voice = ApplicationScenes.Pending(page);
        page.AddVoiceReference(voice.Id, voice.Status);
        var (pages, voices) = await ApplicationScenes.Store(page);
        var useCase = new GetPageUseCase(owner, pages, new FakeCatalog(voices));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new GetPageRequest(page.Id.Value), CancellationToken.None));
    }

    [Fact]
    public async Task Stranger_cannot_update_metadata_and_an_archived_page_stays_unchanged()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var (pages, voices) = await ApplicationScenes.Store(page);
        var unit = new FakeUnitOfWork();
        var stranger = new UpdatePageMetadataUseCase(new FakeCaller { UserId = Guid.NewGuid() }, pages, unit, new FakeCatalog(voices));

        var denied = await Assert.ThrowsAsync<ForbiddenException>(() =>
            stranger.ExecuteAsync(new UpdatePageMetadataRequest(page.Id.Value, "Stolen", null, null), CancellationToken.None));
        Assert.Equal("You cannot change this page.", denied.Message);
        Assert.Equal(0, unit.Saves);

        page.Archive();
        var archived = new UpdatePageMetadataUseCase(owner, pages, unit, new FakeCatalog(voices));
        await Assert.ThrowsAsync<PageArchivedException>(() =>
            archived.ExecuteAsync(new UpdatePageMetadataRequest(page.Id.Value, "Still archived", null, null), CancellationToken.None));
        Assert.Equal(0, unit.Saves);
        Assert.Equal("Show", page.Title.Value);
    }

    [Fact]
    public async Task Archive_freezes_the_page_and_a_second_archive_is_rejected()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var (pages, voices) = await ApplicationScenes.Store(page);
        var unit = new FakeUnitOfWork();
        var useCase = new ArchivePageUseCase(owner, pages, unit, new FakeCatalog(voices));

        var archived = await useCase.ExecuteAsync(new ArchivePageRequest(page.Id.Value), CancellationToken.None);
        Assert.Equal(nameof(PageStatus.Archived), archived.Status);
        Assert.Equal(1, unit.Saves);

        await Assert.ThrowsAsync<PageArchivedException>(() =>
            useCase.ExecuteAsync(new ArchivePageRequest(page.Id.Value), CancellationToken.None));
        Assert.Equal(1, unit.Saves);
    }

    [Fact]
    public async Task Reorder_replaces_only_the_episode_sequence()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var first = ApplicationScenes.Pending(page, "First");
        var second = ApplicationScenes.Pending(page, "Second");
        page.AddVoiceReference(first.Id, first.Status);
        page.AddVoiceReference(second.Id, second.Status);
        var (pages, voices) = await ApplicationScenes.Store(page, first, second);
        var unit = new FakeUnitOfWork();
        var useCase = new ReorderVoicesUseCase(owner, pages, unit, new FakeCatalog(voices));

        var reordered = await useCase.ExecuteAsync(
            new ReorderVoicesRequest(page.Id.Value, [second.Id.Value, first.Id.Value]),
            CancellationToken.None);

        Assert.Equal([second.Id.Value, first.Id.Value], reordered.Voices.Select(voice => voice.VoiceId).ToArray());
        Assert.Equal(1, unit.Saves);

        await Assert.ThrowsAsync<InvalidVoiceOrderException>(() =>
            useCase.ExecuteAsync(new ReorderVoicesRequest(page.Id.Value, [first.Id.Value, first.Id.Value]), CancellationToken.None));
        Assert.Equal(1, unit.Saves);
    }

    [Fact]
    public async Task Publish_with_no_voices_does_not_save_and_a_stranger_cannot_publish()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var (pages, voices) = await ApplicationScenes.Store(page);
        var unit = new FakeUnitOfWork();
        var useCase = new PublishPageUseCase(owner, pages, voices, unit, new FakeCatalog(voices));

        await Assert.ThrowsAsync<PageHasNoVoicesException>(() =>
            useCase.ExecuteAsync(new PublishPageRequest(page.Id.Value), CancellationToken.None));
        Assert.Equal(0, unit.Saves);

        var stranger = new PublishPageUseCase(new FakeCaller { UserId = Guid.NewGuid() }, pages, voices, unit, new FakeCatalog(voices));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            stranger.ExecuteAsync(new PublishPageRequest(page.Id.Value), CancellationToken.None));
        Assert.Equal(0, unit.Saves);
        Assert.Equal(PageStatus.Draft, page.Status);
    }

    [Fact]
    public async Task Publish_names_voices_that_were_uploaded_but_not_added_to_the_order()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var loose = ApplicationScenes.Pending(page);
        var (pages, voices) = await ApplicationScenes.Store(page, loose);
        var unit = new FakeUnitOfWork();
        var useCase = new PublishPageUseCase(owner, pages, voices, unit, new FakeCatalog(voices));

        var exception = await Assert.ThrowsAsync<VoicesNotInEpisodeOrderException>(() =>
            useCase.ExecuteAsync(new PublishPageRequest(page.Id.Value), CancellationToken.None));

        Assert.Contains("not in the episode order", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, unit.Saves);
        Assert.Equal(PageStatus.Draft, page.Status);
    }

    [Fact]
    public async Task Publish_treats_a_missing_voice_row_as_not_ready()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var voice = ApplicationScenes.Ready(page);
        page.AddVoiceReference(voice.Id, voice.Status);
        var (pages, voices) = await ApplicationScenes.Store(page);
        var unit = new FakeUnitOfWork();
        var useCase = new PublishPageUseCase(owner, pages, voices, unit, new FakeCatalog(voices));

        await Assert.ThrowsAsync<VoiceNotReadyForPublishException>(() =>
            useCase.ExecuteAsync(new PublishPageRequest(page.Id.Value), CancellationToken.None));
        Assert.Equal(0, unit.Saves);
        Assert.Equal(PageStatus.Draft, page.Status);
    }

    [Fact]
    public async Task Reorder_rejects_more_than_the_voice_cap_before_it_saves()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var (pages, voices) = await ApplicationScenes.Store(page);
        var unit = new FakeUnitOfWork();
        var useCase = new ReorderVoicesUseCase(owner, pages, unit, new FakeCatalog(voices));
        var tooMany = Enumerable.Range(0, AudioLimits.MaxVoicesPerPage + 1).Select(_ => Guid.NewGuid()).ToArray();

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new ReorderVoicesRequest(page.Id.Value, tooMany), CancellationToken.None));
        Assert.Equal(0, unit.Saves);
    }
}
