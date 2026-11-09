using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Application.Counters;
using Shenok.Back.ContentService.Application.Counters.UseCases.ApplySubscriberDelta;
using Shenok.Back.ContentService.Application.Counters.UseCases.IncrementPlayCount;
using Shenok.Back.ContentService.Application.Pages.UseCases.CreatePage;
using Shenok.Back.ContentService.Application.Pages.UseCases.PublishPage;
using Shenok.Back.ContentService.Application.Pages.UseCases.UpdatePageMetadata;
using Shenok.Back.ContentService.Application.Playback;
using Shenok.Back.ContentService.Application.Playback.UseCases.IssuePlaylist;
using Shenok.Back.ContentService.Application.Voices.UseCases.AddVoiceToPage;
using Shenok.Back.ContentService.Application.Voices.UseCases.PackageVoice;
using Shenok.Back.ContentService.Application.Voices.UseCases.UploadVoice;
using Shenok.Back.ContentService.Application.Waveforms.UseCases.RegisterWaveform;
using Shenok.Back.ContentService.Application.Waveforms.UseCases.RevokeWaveform;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Exceptions;
using WaveformBars = Shenok.Back.WaveformCodec.WaveformCodec;

namespace Shenok.Back.ContentService.Application.Tests;

public class ContentUseCaseTests
{
    [Fact]
    public async Task Create_page_uses_the_caller_as_owner()
    {
        var caller = new FakeCaller();
        var pages = new FakePages();
        var unit = new FakeUnitOfWork();
        var useCase = new CreatePageUseCase(caller, pages, unit);

        var created = await useCase.ExecuteAsync(new CreatePageRequest("Night Walks", "A quiet show.", null), CancellationToken.None);

        Assert.Equal(caller.UserId, created.OwnerId);
        Assert.Equal(nameof(PageStatus.Draft), created.Status);
        Assert.Empty(created.Voices);
        Assert.Equal(1, unit.Saves);
    }

    [Fact]
    public async Task Listener_cannot_create_a_page()
    {
        var caller = new FakeCaller { IsContentCreator = false };
        var unit = new FakeUnitOfWork();
        var useCase = new CreatePageUseCase(caller, new FakePages(), unit);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(new CreatePageRequest("Night Walks", null, null), CancellationToken.None));
        Assert.Equal(0, unit.Saves);
    }

    [Fact]
    public async Task Admin_can_update_a_page_they_do_not_own()
    {
        var owner = OwnerId.New();
        var page = Page.CreateDraft(owner, PageTitle.Create("Old"), PageDescription.Create(null), null);
        var pages = new FakePages();
        await pages.AddAsync(page);
        var caller = new FakeCaller { IsAdmin = true, UserId = Guid.NewGuid() };
        var unit = new FakeUnitOfWork();
        var useCase = new UpdatePageMetadataUseCase(caller, pages, new FakeVoices(), unit);

        var updated = await useCase.ExecuteAsync(
            new UpdatePageMetadataRequest(page.Id.Value, "New", "Updated", null),
            CancellationToken.None);

        Assert.Equal("New", updated.Title);
        Assert.Equal(owner.Value, updated.OwnerId);
        Assert.Equal(1, unit.Saves);
    }

    [Fact]
    public async Task Publish_rejects_a_pending_voice_and_maps_a_second_publish_to_conflict()
    {
        var caller = new FakeCaller();
        var page = Page.CreateDraft(OwnerId.From(caller.UserId), PageTitle.Create("Show"), PageDescription.Create(null), null);
        var pending = Voice.CreatePending(page.Id, VoiceTitle.Create("One"), "sources/a", "audio/wav", 10);
        page.AddVoiceReference(pending.Id, pending.Status);
        var pages = new FakePages();
        var voices = new FakeVoices();
        await pages.AddAsync(page);
        voices.Add(pending);
        var unit = new FakeUnitOfWork();
        var useCase = new PublishPageUseCase(caller, pages, voices, unit);

        await Assert.ThrowsAsync<VoiceNotReadyForPublishException>(() =>
            useCase.ExecuteAsync(new PublishPageRequest(page.Id.Value), CancellationToken.None));
        Assert.Equal(0, unit.Saves);

        pending.CompletePackagingSuccess(8, "hls/playlist.m3u8", "hls/seg/");
        var published = await useCase.ExecuteAsync(new PublishPageRequest(page.Id.Value), CancellationToken.None);
        Assert.Equal(nameof(PageStatus.Published), published.Status);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new PublishPageRequest(page.Id.Value), CancellationToken.None));
        Assert.Equal(1, unit.Saves);
    }

    [Fact]
    public async Task Upload_checks_the_page_before_writing_a_blob()
    {
        var caller = new FakeCaller();
        var page = Page.CreateDraft(OwnerId.From(caller.UserId), PageTitle.Create("Show"), PageDescription.Create(null), null);
        page.Archive();
        var pages = new FakePages();
        await pages.AddAsync(page);
        var blobs = new FakeBlobs();
        var useCase = new UploadVoiceUseCase(caller, pages, new FakeVoices(), blobs, new FakeUnitOfWork());

        await Assert.ThrowsAsync<PageArchivedException>(() =>
            useCase.ExecuteAsync(Upload(page.Id.Value, size: 32), CancellationToken.None));
        Assert.Empty(blobs.Puts);

        var stranger = new FakeCaller { UserId = Guid.NewGuid() };
        var open = Page.CreateDraft(OwnerId.From(caller.UserId), PageTitle.Create("Show"), PageDescription.Create(null), null);
        await pages.AddAsync(open);
        var denied = new UploadVoiceUseCase(stranger, pages, new FakeVoices(), blobs, new FakeUnitOfWork());
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            denied.ExecuteAsync(Upload(open.Id.Value, size: 32), CancellationToken.None));
        Assert.Empty(blobs.Puts);

        var owned = new UploadVoiceUseCase(caller, pages, new FakeVoices(), blobs, new FakeUnitOfWork());
        await Assert.ThrowsAsync<InvalidAudioAssetException>(() =>
            owned.ExecuteAsync(Upload(open.Id.Value, size: 0), CancellationToken.None));
        Assert.Empty(blobs.Puts);
    }

    [Fact]
    public async Task Upload_stores_a_random_key_and_deletes_it_when_save_fails()
    {
        var caller = new FakeCaller();
        var page = Page.CreateDraft(OwnerId.From(caller.UserId), PageTitle.Create("Show"), PageDescription.Create(null), null);
        var pages = new FakePages();
        await pages.AddAsync(page);
        var blobs = new FakeBlobs();
        var unit = new FakeUnitOfWork { Failure = new InvalidOperationException("database") };
        var useCase = new UploadVoiceUseCase(caller, pages, new FakeVoices(), blobs, unit);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ExecuteAsync(Upload(page.Id.Value, size: 32), CancellationToken.None));

        var key = Assert.Single(blobs.Puts);
        Assert.StartsWith("sources/", key);
        Assert.DoesNotContain("Episode", key);
        Assert.Equal(key, Assert.Single(blobs.Deletes));
    }

    [Fact]
    public async Task Published_page_rejects_a_pending_voice()
    {
        var caller = new FakeCaller();
        var page = Page.CreateDraft(OwnerId.From(caller.UserId), PageTitle.Create("Show"), PageDescription.Create(null), null);
        var ready = Ready(page.Id);
        page.AddVoiceReference(ready.Id, ready.Status);
        page.Publish(new Dictionary<VoiceId, PackagingStatus> { [ready.Id] = ready.Status });
        var pending = Voice.CreatePending(page.Id, VoiceTitle.Create("Two"), "sources/b", "audio/mpeg", 10);
        var pages = new FakePages();
        var voices = new FakeVoices();
        await pages.AddAsync(page);
        voices.Add(ready);
        voices.Add(pending);
        var unit = new FakeUnitOfWork();
        var useCase = new AddVoiceToPageUseCase(caller, pages, voices, unit);

        await Assert.ThrowsAsync<VoiceNotReadyForPublishException>(() =>
            useCase.ExecuteAsync(new AddVoiceToPageRequest(page.Id.Value, pending.Id.Value), CancellationToken.None));
        Assert.Equal(0, unit.Saves);
    }

    [Fact]
    public async Task Package_uses_the_probed_duration_and_skips_a_ready_voice()
    {
        var caller = new FakeCaller { IsInternalService = true };
        var page = Page.CreateDraft(OwnerId.New(), PageTitle.Create("Show"), PageDescription.Create(null), null);
        var pending = Voice.CreatePending(page.Id, VoiceTitle.Create("One"), "sources/a", "audio/wav", 10);
        var ready = Ready(page.Id, "Two");
        var voices = new FakeVoices();
        voices.Add(pending);
        voices.Add(ready);
        var packager = new FakePackager();
        var unit = new FakeUnitOfWork();
        var useCase = new PackageVoiceUseCase(caller, voices, packager, unit);

        var packaged = await useCase.ExecuteAsync(new PackageVoiceRequest(pending.Id.Value), CancellationToken.None);
        Assert.Equal(nameof(PackagingStatus.Ready), packaged.PackagingStatus);
        Assert.Equal(12.5, packaged.DurationSeconds);
        Assert.Equal(AudioLimits.MaxDurationSeconds, packager.SeenMaxDuration);

        packager.Result = new AudioPackageResult.Succeeded(AudioLimits.MaxDurationSeconds + 1, "hls/playlist.m3u8", "hls/seg/");
        var tooLong = Voice.CreatePending(page.Id, VoiceTitle.Create("Three"), "sources/c", "audio/wav", 10);
        voices.Add(tooLong);
        var failed = await useCase.ExecuteAsync(new PackageVoiceRequest(tooLong.Id.Value), CancellationToken.None);
        Assert.Equal(nameof(PackagingStatus.Failed), failed.PackagingStatus);
        Assert.Null(failed.DurationSeconds);

        var callsBefore = packager.Calls;
        var again = await useCase.ExecuteAsync(new PackageVoiceRequest(ready.Id.Value), CancellationToken.None);
        Assert.Equal(nameof(PackagingStatus.Ready), again.PackagingStatus);
        Assert.Equal(callsBefore, packager.Calls);
    }

    [Fact]
    public async Task Package_is_internal()
    {
        var voices = new FakeVoices();
        var packager = new FakePackager();
        var useCase = new PackageVoiceUseCase(new FakeCaller(), voices, packager, new FakeUnitOfWork());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(new PackageVoiceRequest(Guid.NewGuid()), CancellationToken.None));
        Assert.Equal(0, packager.Calls);
    }

    [Fact]
    public async Task Register_returns_bars_that_decode_to_the_assigned_code()
    {
        var caller = new FakeCaller();
        var page = Page.CreateDraft(OwnerId.From(caller.UserId), PageTitle.Create("Show"), PageDescription.Create(null), null);
        var pages = new FakePages();
        await pages.AddAsync(page);
        var codes = new FakeCodes();
        var useCase = new RegisterWaveformUseCase(caller, pages, new FakeVoices(), new FakeMappings(), codes, new FakeUnitOfWork());

        var registered = await useCase.ExecuteAsync(
            new RegisterWaveformRequest(nameof(WaveformTargetType.Page), page.Id.Value),
            CancellationToken.None);

        Assert.Equal(7, registered.Code);
        Assert.Equal(WaveformBars.BarCount, registered.Bars.Count);
        Assert.True(WaveformBars.TryDecode(registered.Bars.Select(bar => (byte)bar).ToArray(), out var decoded));
        Assert.Equal((ulong)registered.Code, decoded);
        Assert.Equal(1, codes.Calls);
    }

    [Fact]
    public async Task Register_does_not_allocate_for_an_archived_page_or_a_voice_that_is_not_attached()
    {
        var caller = new FakeCaller();
        var page = Page.CreateDraft(OwnerId.From(caller.UserId), PageTitle.Create("Show"), PageDescription.Create(null), null);
        page.Archive();
        var pages = new FakePages();
        await pages.AddAsync(page);
        var codes = new FakeCodes();
        var useCase = new RegisterWaveformUseCase(caller, pages, new FakeVoices(), new FakeMappings(), codes, new FakeUnitOfWork());

        await Assert.ThrowsAsync<PageArchivedException>(() =>
            useCase.ExecuteAsync(new RegisterWaveformRequest(nameof(WaveformTargetType.Page), page.Id.Value), CancellationToken.None));
        Assert.Equal(0, codes.Calls);

        var open = Page.CreateDraft(OwnerId.From(caller.UserId), PageTitle.Create("Show"), PageDescription.Create(null), null);
        await pages.AddAsync(open);
        var voice = Voice.CreatePending(open.Id, VoiceTitle.Create("One"), "sources/a", "audio/wav", 10);
        var voices = new FakeVoices();
        voices.Add(voice);
        var unattached = new RegisterWaveformUseCase(caller, pages, voices, new FakeMappings(), codes, new FakeUnitOfWork());
        await Assert.ThrowsAsync<ConflictException>(() =>
            unattached.ExecuteAsync(new RegisterWaveformRequest(nameof(WaveformTargetType.Voice), voice.Id.Value), CancellationToken.None));
        Assert.Equal(0, codes.Calls);
    }

    [Fact]
    public async Task Register_retries_when_the_code_is_already_reserved()
    {
        var caller = new FakeCaller();
        var page = Page.CreateDraft(OwnerId.From(caller.UserId), PageTitle.Create("Show"), PageDescription.Create(null), null);
        var pages = new FakePages();
        await pages.AddAsync(page);
        var mappings = new FakeMappings { RejectNextCode = true };
        var codes = new FakeCodes();
        var useCase = new RegisterWaveformUseCase(caller, pages, new FakeVoices(), mappings, codes, new FakeUnitOfWork());

        var registered = await useCase.ExecuteAsync(
            new RegisterWaveformRequest(nameof(WaveformTargetType.Page), page.Id.Value),
            CancellationToken.None);

        Assert.Equal(8, registered.Code);
        Assert.Equal(2, codes.Calls);
        Assert.Equal(1, mappings.Adds);
    }

    [Fact]
    public async Task Revoke_is_owner_scoped_and_a_second_revoke_conflicts()
    {
        var caller = new FakeCaller();
        var page = Page.CreateDraft(OwnerId.From(caller.UserId), PageTitle.Create("Show"), PageDescription.Create(null), null);
        var mapping = WaveformMapping.RegisterForPage(WaveformCode.From(9), page.Id, LayoutVersion.V1, pageIsArchived: false);
        var pages = new FakePages();
        var mappings = new FakeMappings();
        await pages.AddAsync(page);
        await mappings.AddAsync(mapping);
        var unit = new FakeUnitOfWork();
        var useCase = new RevokeWaveformUseCase(caller, pages, new FakeVoices(), mappings, unit);

        var revoked = await useCase.ExecuteAsync(new RevokeWaveformRequest(mapping.Id.Value), CancellationToken.None);
        Assert.Equal(nameof(WaveformMappingStatus.Revoked), revoked.Status);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new RevokeWaveformRequest(mapping.Id.Value), CancellationToken.None));

        var stranger = new FakeCaller { UserId = Guid.NewGuid() };
        var other = WaveformMapping.RegisterForPage(WaveformCode.From(10), page.Id, LayoutVersion.V1, pageIsArchived: false);
        await mappings.AddAsync(other);
        var denied = new RevokeWaveformUseCase(stranger, pages, new FakeVoices(), mappings, unit);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            denied.ExecuteAsync(new RevokeWaveformRequest(other.Id.Value), CancellationToken.None));
        Assert.True(other.IsActive);
    }

    [Fact]
    public async Task Issue_playlist_signs_an_active_scan_and_rejects_a_revoked_or_foreign_mapping()
    {
        var caller = new FakeCaller { IsInternalService = true };
        var page = Page.CreateDraft(OwnerId.New(), PageTitle.Create("Show"), PageDescription.Create(null), null);
        var voice = Ready(page.Id);
        page.AddVoiceReference(voice.Id, voice.Status);
        var active = WaveformMapping.RegisterForPage(WaveformCode.From(3), page.Id, LayoutVersion.V1, false);
        var revoked = WaveformMapping.RegisterForVoice(WaveformCode.From(4), voice.Id, LayoutVersion.V1, false);
        revoked.Revoke();
        var otherPage = Page.CreateDraft(OwnerId.New(), PageTitle.Create("Other"), PageDescription.Create(null), null);
        var foreign = WaveformMapping.RegisterForPage(WaveformCode.From(5), otherPage.Id, LayoutVersion.V1, false);
        var pages = new FakePages();
        var voices = new FakeVoices();
        var mappings = new FakeMappings();
        await pages.AddAsync(page);
        await pages.AddAsync(otherPage);
        voices.Add(voice);
        await mappings.AddAsync(active);
        await mappings.AddAsync(revoked);
        await mappings.AddAsync(foreign);
        var blobs = new FakeBlobs();
        var signer = new HlsPlaylistSigner(
            new PlaybackSigningSettings("https://gateway.example", "dev-only-signing-key-32-characters"),
            new FixedClock(new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero)));
        var useCase = new IssuePlaylistUseCase(caller, voices, pages, mappings, blobs, signer);

        var issued = await useCase.ExecuteAsync(new IssuePlaylistRequest(voice.Id.Value, active.Id.Value), CancellationToken.None);
        Assert.Contains("sig=", issued.Playlist, StringComparison.Ordinal);
        Assert.DoesNotContain("hls/seg", issued.Playlist, StringComparison.Ordinal);
        Assert.DoesNotContain(voice.Audio.PlaylistStorageKey!, issued.Playlist, StringComparison.Ordinal);
        Assert.Equal(1, blobs.Reads);

        var revokedPlayback = await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new IssuePlaylistRequest(voice.Id.Value, revoked.Id.Value), CancellationToken.None));
        Assert.Contains("not active", revokedPlayback.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(new IssuePlaylistRequest(voice.Id.Value, foreign.Id.Value), CancellationToken.None));
        Assert.Equal(1, blobs.Reads);

        var outsider = new IssuePlaylistUseCase(new FakeCaller(), voices, pages, mappings, blobs, signer);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            outsider.ExecuteAsync(new IssuePlaylistRequest(voice.Id.Value, null), CancellationToken.None));
        Assert.Equal(1, blobs.Reads);
    }

    [Fact]
    public async Task Counter_commands_do_not_change_counts()
    {
        var caller = new FakeCaller { IsInternalService = true };
        var page = Page.CreateDraft(OwnerId.New(), PageTitle.Create("Show"), PageDescription.Create(null), null);
        var voice = Ready(page.Id);
        var pages = new FakePages();
        var voiceStore = new FakeVoices();
        await pages.AddAsync(page);
        voiceStore.Add(voice);
        var subscribers = new ApplySubscriberDeltaUseCase(caller, pages);
        var plays = new IncrementPlayCountUseCase(caller, voiceStore);

        await Assert.ThrowsAsync<CounterUpdateDeferredException>(() =>
            subscribers.ExecuteAsync(new ApplySubscriberDeltaRequest(page.Id.Value, 1, "user:page"), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            subscribers.ExecuteAsync(new ApplySubscriberDeltaRequest(Guid.NewGuid(), 1, "user:missing"), CancellationToken.None));
        await Assert.ThrowsAsync<CounterUpdateDeferredException>(() =>
            plays.ExecuteAsync(new IncrementPlayCountRequest(voice.Id.Value, Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            plays.ExecuteAsync(new IncrementPlayCountRequest(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() =>
            subscribers.ExecuteAsync(new ApplySubscriberDeltaRequest(page.Id.Value, 5, "user:page"), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ApplySubscriberDeltaUseCase(new FakeCaller(), pages).ExecuteAsync(
                new ApplySubscriberDeltaRequest(page.Id.Value, 1, "user:page"),
                CancellationToken.None));
    }

    private static UploadVoiceRequest Upload(Guid pageId, long size) =>
        new(pageId, "Episode 1", "audio/wav", size, new MemoryStream(size <= 0 ? [] : new byte[size]));

    private static Voice Ready(PageId pageId, string title = "Episode 1")
    {
        var voice = Voice.CreatePending(pageId, VoiceTitle.Create(title), "sources/ready", "audio/wav", 10);
        voice.CompletePackagingSuccess(9, "hls/playlist.m3u8", "hls/seg/");
        return voice;
    }
}
