using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.DomainEvents;
using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Domain.Tests;

public class PageTests
{
    [Fact]
    public void CreateDraft_starts_empty_as_draft_and_raises_created()
    {
        var owner = OwnerId.New();
        var page = ContentFixtures.DraftPage(owner);

        Assert.Equal(PageStatus.Draft, page.Status);
        Assert.Empty(page.VoiceIds);
        Assert.Equal(owner, page.OwnerId);
        Assert.Equal("Night Walks", page.Title.Value);
        Assert.NotEqual(default, page.CreatedAt);
        Assert.Single(page.DomainEvents.OfType<PageCreatedDomainEvent>());
    }

    [Fact]
    public void Publish_rejects_when_page_has_no_voices()
    {
        var page = ContentFixtures.DraftPage();

        var ex = Assert.Throws<PageHasNoVoicesException>(() =>
            page.Publish(new Dictionary<VoiceId, PackagingStatus>()));

        Assert.Equal(page.Id, ex.PageId);
        Assert.Equal(PageStatus.Draft, page.Status);
    }

    [Theory]
    [InlineData(PackagingStatus.Pending)]
    [InlineData(PackagingStatus.Failed)]
    public void Publish_rejects_when_any_voice_is_pending_or_failed(PackagingStatus status)
    {
        var page = ContentFixtures.DraftPage();
        var ready = VoiceId.New();
        var blocked = VoiceId.New();
        page.AddVoice(ready);
        page.AddVoice(blocked);

        var ex = Assert.Throws<VoiceNotReadyForPublishException>(() =>
            page.Publish(ContentFixtures.Readiness(
                (ready, PackagingStatus.Ready),
                (blocked, status))));

        Assert.Equal(blocked, ex.VoiceId);
        Assert.Equal(status, ex.PackagingStatus);
        Assert.Equal(PageStatus.Draft, page.Status);
    }

    [Fact]
    public void Publish_rejects_when_a_referenced_voice_is_missing_from_readiness()
    {
        var page = ContentFixtures.DraftPage();
        var onPage = VoiceId.New();
        var other = VoiceId.New();
        page.AddVoice(onPage);

        var ex = Assert.Throws<VoiceNotReadyForPublishException>(() =>
            page.Publish(ContentFixtures.Readiness((other, PackagingStatus.Ready))));

        Assert.Equal(onPage, ex.VoiceId);
        Assert.Null(ex.PackagingStatus);
        Assert.Equal(PageStatus.Draft, page.Status);
    }

    [Fact]
    public void Publish_succeeds_when_every_referenced_voice_is_ready()
    {
        var page = ContentFixtures.DraftPage();
        var first = VoiceId.New();
        var second = VoiceId.New();
        page.AddVoice(first);
        page.AddVoice(second);

        page.Publish(ContentFixtures.Readiness(
            (first, PackagingStatus.Ready),
            (second, PackagingStatus.Ready),
            (VoiceId.New(), PackagingStatus.Pending)));

        Assert.Equal(PageStatus.Published, page.Status);
        Assert.Single(page.DomainEvents.OfType<PagePublishedDomainEvent>());
    }

    [Fact]
    public void Publish_rejects_when_already_published()
    {
        var page = PublishedPageWithOneReadyVoice();

        Assert.Throws<PageAlreadyPublishedException>(() =>
            page.Publish(ContentFixtures.Readiness((page.VoiceIds[0], PackagingStatus.Ready))));
    }

    [Fact]
    public void Publish_rejects_an_archived_page()
    {
        var page = ContentFixtures.DraftPage();
        var voiceId = VoiceId.New();
        page.AddVoice(voiceId);
        page.Archive();

        Assert.Throws<CannotPublishArchivedPageException>(() =>
            page.Publish(ContentFixtures.Readiness((voiceId, PackagingStatus.Ready))));
        Assert.Equal(PageStatus.Archived, page.Status);
    }

    [Fact]
    public void Reorder_replaces_order_when_the_voice_set_is_unchanged()
    {
        var page = ContentFixtures.DraftPage();
        var a = VoiceId.New();
        var b = VoiceId.New();
        var c = VoiceId.New();
        page.AddVoice(a);
        page.AddVoice(b);
        page.AddVoice(c);

        page.ReorderVoices([c, a, b]);

        Assert.Equal([c, a, b], page.VoiceIds);
    }

    [Fact]
    public void Reorder_rejects_an_unknown_voice_id()
    {
        var page = ContentFixtures.DraftPage();
        var a = VoiceId.New();
        var b = VoiceId.New();
        page.AddVoice(a);
        page.AddVoice(b);

        Assert.Throws<InvalidVoiceOrderException>(() =>
            page.ReorderVoices([a, VoiceId.New()]));
        Assert.Equal([a, b], page.VoiceIds);
    }

    [Fact]
    public void Reorder_rejects_duplicate_ids()
    {
        var page = ContentFixtures.DraftPage();
        var a = VoiceId.New();
        var b = VoiceId.New();
        page.AddVoice(a);
        page.AddVoice(b);

        Assert.Throws<InvalidVoiceOrderException>(() =>
            page.ReorderVoices([a, a]));
        Assert.Equal([a, b], page.VoiceIds);
    }

    [Fact]
    public void Reorder_rejects_a_list_that_drops_a_voice()
    {
        var page = ContentFixtures.DraftPage();
        var a = VoiceId.New();
        var b = VoiceId.New();
        page.AddVoice(a);
        page.AddVoice(b);

        Assert.Throws<InvalidVoiceOrderException>(() =>
            page.ReorderVoices([a]));
        Assert.Equal([a, b], page.VoiceIds);
    }

    [Fact]
    public void Archive_freezes_episode_order_and_blocks_later_reorder()
    {
        var page = ContentFixtures.DraftPage();
        var a = VoiceId.New();
        var b = VoiceId.New();
        page.AddVoice(a);
        page.AddVoice(b);
        var frozen = page.VoiceIds.ToArray();

        page.Archive();

        Assert.Throws<PageArchivedException>(() => page.ReorderVoices([b, a]));
        Assert.Equal(frozen, page.VoiceIds);
        Assert.Single(page.DomainEvents.OfType<PageArchivedDomainEvent>());
    }

    [Fact]
    public void Archive_blocks_add_voice_update_metadata_and_a_second_archive()
    {
        var page = ContentFixtures.DraftPage();
        page.Archive();

        Assert.Throws<PageArchivedException>(() => page.AddVoice(VoiceId.New()));
        Assert.Throws<PageArchivedException>(() =>
            page.UpdateMetadata(PageTitle.Create("Later"), PageDescription.Create("no"), null));
        Assert.Throws<PageArchivedException>(() => page.Archive());
        Assert.Empty(page.VoiceIds);
        Assert.Equal("Night Walks", page.Title.Value);
    }

    [Fact]
    public void UpdateMetadata_is_allowed_on_a_published_page()
    {
        var page = PublishedPageWithOneReadyVoice();

        page.UpdateMetadata(
            PageTitle.Create("Retitled"),
            PageDescription.Create("Edited after publish."),
            CoverImageUrl.Create("https://cdn.example.com/new.jpg"));

        Assert.Equal("Retitled", page.Title.Value);
        Assert.Equal(PageStatus.Published, page.Status);
    }

    [Fact]
    public void AddVoiceReference_appends_and_rejects_a_duplicate()
    {
        var page = ContentFixtures.DraftPage();
        var voiceId = VoiceId.New();

        page.AddVoice(voiceId);

        Assert.Equal([voiceId], page.VoiceIds);
        Assert.Throws<DuplicateVoiceOnPageException>(() => page.AddVoice(voiceId));
    }

    [Theory]
    [InlineData(PackagingStatus.Pending)]
    [InlineData(PackagingStatus.Failed)]
    public void Published_page_rejects_a_voice_that_is_not_ready(PackagingStatus status)
    {
        var page = PublishedPageWithOneReadyVoice();
        var extra = VoiceId.New();

        var ex = Assert.Throws<VoiceNotReadyForPublishException>(() => page.AddVoice(extra, status));

        Assert.Equal(extra, ex.VoiceId);
        Assert.Equal(status, ex.PackagingStatus);
        Assert.Single(page.VoiceIds);
        Assert.Equal(PageStatus.Published, page.Status);
    }

    [Fact]
    public void Published_page_can_receive_a_ready_voice()
    {
        var page = PublishedPageWithOneReadyVoice();
        var extra = VoiceId.New();

        page.AddVoice(extra, PackagingStatus.Ready);

        Assert.Equal(PageStatus.Published, page.Status);
        Assert.Equal(2, page.VoiceIds.Count);
        Assert.Contains(extra, page.VoiceIds);
    }

    [Fact]
    public void EnsureOwnedBy_rejects_a_different_owner()
    {
        var owner = OwnerId.New();
        var page = ContentFixtures.DraftPage(owner);

        page.EnsureOwnedBy(owner);
        var ex = Assert.Throws<NotPageOwnerException>(() => page.EnsureOwnedBy(OwnerId.New()));
        Assert.Equal(page.Id, ex.PageId);
    }

    [Fact]
    public void Failed_voice_cannot_publish_the_page()
    {
        var page = ContentFixtures.DraftPage();
        var failed = ContentFixtures.FailedVoice(page.Id);
        page.AddVoice(failed);

        Assert.Throws<VoiceNotReadyForPublishException>(() =>
            page.Publish(ContentFixtures.Readiness((failed.Id, failed.Status))));
        Assert.Equal(PackagingStatus.Failed, failed.Status);
        Assert.Equal(PageStatus.Draft, page.Status);
    }

    private static Page PublishedPageWithOneReadyVoice()
    {
        var page = ContentFixtures.DraftPage();
        var voice = ContentFixtures.ReadyVoice(page.Id);
        page.AddVoice(voice);
        page.Publish(ContentFixtures.Readiness((voice.Id, voice.Status)));
        return page;
    }
}
