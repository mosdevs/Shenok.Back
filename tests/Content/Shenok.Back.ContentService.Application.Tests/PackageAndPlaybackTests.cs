using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Application.Playback;
using Shenok.Back.ContentService.Application.Playback.UseCases.IssuePlaylist;
using Shenok.Back.ContentService.Application.Voices.UseCases.PackageVoice;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Application.Tests;

public class PackageAndPlaybackTests
{
    [Fact]
    public async Task Package_failure_marks_pending_failed_and_a_later_success_retries()
    {
        var caller = new FakeCaller { IsInternalService = true };
        var page = ApplicationScenes.Draft(new FakeCaller());
        var voice = ApplicationScenes.Pending(page);
        var voices = new FakeVoices();
        voices.Add(voice);
        var packager = new FakePackager { Result = new AudioPackageResult.Failed("ffprobe failed") };
        var unit = new FakeUnitOfWork();
        var useCase = new PackageVoiceUseCase(caller, voices, packager, unit);

        var failed = await useCase.ExecuteAsync(new PackageVoiceRequest(voice.Id.Value), CancellationToken.None);
        Assert.Equal(nameof(PackagingStatus.Failed), failed.PackagingStatus);
        Assert.Null(failed.DurationSeconds);
        Assert.Equal(1, unit.Saves);

        packager.Result = new AudioPackageResult.Succeeded(40, "hls/playlist.m3u8", "hls/seg/");
        var retried = await useCase.ExecuteAsync(new PackageVoiceRequest(voice.Id.Value), CancellationToken.None);
        Assert.Equal(nameof(PackagingStatus.Ready), retried.PackagingStatus);
        Assert.Equal(40, retried.DurationSeconds);
        Assert.Equal(2, unit.Saves);
    }

    [Fact]
    public async Task Package_returns_ready_when_save_loses_the_race_and_rethrows_when_reload_is_not_ready()
    {
        var caller = new FakeCaller { IsInternalService = true };
        var page = ApplicationScenes.Draft(new FakeCaller());
        var voice = ApplicationScenes.Pending(page);
        var voices = new FakeVoices();
        voices.Add(voice);
        var conflict = new InvalidPackagingTransitionException(PackagingStatus.Pending, PackagingStatus.Ready);
        var lost = new FakeUnitOfWork { Failure = conflict };
        var useCase = new PackageVoiceUseCase(caller, voices, new FakePackager(), lost);

        var ready = await useCase.ExecuteAsync(new PackageVoiceRequest(voice.Id.Value), CancellationToken.None);

        Assert.Equal(voice.Id.Value, ready.VoiceId);
        Assert.Equal(nameof(PackagingStatus.Ready), ready.PackagingStatus);
        Assert.Equal(0, lost.Saves);

        var hidden = ApplicationScenes.Pending(page, "Hidden");
        var hiddenVoices = new FakeVoices { HideAfterFirstGet = true };
        hiddenVoices.Add(hidden);
        var hiddenUnit = new FakeUnitOfWork { Failure = conflict };
        var hiddenUseCase = new PackageVoiceUseCase(caller, hiddenVoices, new FakePackager(), hiddenUnit);

        await Assert.ThrowsAsync<InvalidPackagingTransitionException>(() =>
            hiddenUseCase.ExecuteAsync(new PackageVoiceRequest(hidden.Id.Value), CancellationToken.None));
        Assert.Equal(2, hiddenVoices.Gets);
    }

    [Fact]
    public async Task A_second_package_failure_leaves_the_failed_voice_unchanged()
    {
        var caller = new FakeCaller { IsInternalService = true };
        var page = ApplicationScenes.Draft(new FakeCaller());
        var voice = ApplicationScenes.Pending(page);
        voice.CompletePackagingFailure();
        var voices = new FakeVoices();
        voices.Add(voice);
        var packager = new FakePackager { Result = new AudioPackageResult.Failed("still broken") };
        var unit = new FakeUnitOfWork();
        var useCase = new PackageVoiceUseCase(caller, voices, packager, unit);

        var result = await useCase.ExecuteAsync(new PackageVoiceRequest(voice.Id.Value), CancellationToken.None);

        Assert.Equal(nameof(PackagingStatus.Failed), result.PackagingStatus);
        Assert.Equal(1, packager.Calls);
        Assert.Equal(0, unit.Saves);
    }

    [Fact]
    public async Task Issue_playlist_without_a_scan_signs_the_ready_voice()
    {
        var (useCase, blobs, voice, _) = await PlaybackScene();

        var issued = await useCase.ExecuteAsync(new IssuePlaylistRequest(voice.Id.Value, null), CancellationToken.None);

        Assert.Equal(voice.Id.Value, issued.VoiceId);
        Assert.Equal(9, issued.DurationSeconds);
        Assert.Contains("sig=", issued.Playlist, StringComparison.Ordinal);
        Assert.DoesNotContain(voice.Audio.PlaylistStorageKey!, issued.Playlist, StringComparison.Ordinal);
        Assert.Equal(1, blobs.Reads);
        AssertSignature(issued.Playlist, voice.Id.Value);
    }

    [Fact]
    public async Task Issue_playlist_rejects_pending_audio_and_a_voice_that_is_not_attached()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var pending = ApplicationScenes.Pending(page);
        var loose = ApplicationScenes.Ready(page, "Loose");
        page.AddVoiceReference(pending.Id, pending.Status);
        var (pages, voices) = await ApplicationScenes.Store(page, pending, loose);
        var blobs = new FakeBlobs();
        var useCase = new IssuePlaylistUseCase(InternalCaller(), voices, pages, new FakeMappings(), blobs, Signer());

        await Assert.ThrowsAsync<AudioNotReadyException>(() =>
            useCase.ExecuteAsync(new IssuePlaylistRequest(pending.Id.Value, null), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(new IssuePlaylistRequest(loose.Id.Value, null), CancellationToken.None));
        Assert.Equal(0, blobs.Reads);
    }

    [Fact]
    public async Task A_new_scan_of_an_archived_page_is_rejected_and_a_non_scan_play_still_signs()
    {
        var (useCase, blobs, voice, mapping) = await PlaybackScene(archive: true);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(new IssuePlaylistRequest(voice.Id.Value, mapping.Id.Value), CancellationToken.None));
        Assert.Equal(0, blobs.Reads);

        var issued = await useCase.ExecuteAsync(new IssuePlaylistRequest(voice.Id.Value, null), CancellationToken.None);
        Assert.Contains("sig=", issued.Playlist, StringComparison.Ordinal);
        Assert.Equal(1, blobs.Reads);
    }

    [Fact]
    public async Task An_empty_waveform_id_is_not_treated_as_a_direct_play()
    {
        var (useCase, blobs, voice, _) = await PlaybackScene();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new IssuePlaylistRequest(voice.Id.Value, Guid.Empty), CancellationToken.None));
        Assert.Equal(0, blobs.Reads);
    }

    private static async Task<(IssuePlaylistUseCase UseCase, FakeBlobs Blobs, Voice Voice, WaveformMapping Mapping)> PlaybackScene(bool archive = false)
    {
        var page = ApplicationScenes.Draft(new FakeCaller());
        var voice = ApplicationScenes.Ready(page);
        page.AddVoiceReference(voice.Id, voice.Status);
        var mapping = WaveformMapping.RegisterForPage(WaveformCode.From(11), page.Id, LayoutVersion.V1, pageIsArchived: false);
        if (archive)
            page.Archive();

        var (pages, voices) = await ApplicationScenes.Store(page, voice);
        var mappings = new FakeMappings();
        await mappings.AddAsync(mapping);
        var blobs = new FakeBlobs();
        var useCase = new IssuePlaylistUseCase(InternalCaller(), voices, pages, mappings, blobs, Signer());
        return (useCase, blobs, voice, mapping);
    }

    private static FakeCaller InternalCaller() => new() { IsInternalService = true, IsContentCreator = false };

    private static HlsPlaylistSigner Signer() =>
        new(
            new PlaybackSigningSettings("https://gateway.example", "dev-only-signing-key-32-characters"),
            new FixedClock(new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero)));

    private static void AssertSignature(string playlist, Guid voiceId)
    {
        var url = playlist.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("https://", StringComparison.Ordinal));
        var uri = new Uri(url);
        var query = uri.Query.TrimStart('?').Split('&');
        var expires = long.Parse(query.Single(part => part.StartsWith("exp=", StringComparison.Ordinal))["exp=".Length..]);
        var signature = query.Single(part => part.StartsWith("sig=", StringComparison.Ordinal))["sig=".Length..];
        Assert.True(PlaybackSignature.Matches("dev-only-signing-key-32-characters", uri.AbsolutePath, expires, signature));
        Assert.Contains(voiceId.ToString("D"), uri.AbsolutePath, StringComparison.Ordinal);
    }
}
