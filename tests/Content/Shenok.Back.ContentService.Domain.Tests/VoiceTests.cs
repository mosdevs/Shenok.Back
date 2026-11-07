using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Domain.Tests;

public class VoiceTests
{
    [Fact]
    public void CreatePending_has_no_duration_and_pending_packaging()
    {
        var pageId = PageId.New();
        var voice = ContentFixtures.PendingVoice(pageId);

        Assert.Equal(pageId, voice.PageId);
        Assert.Equal("Episode 1", voice.Title.Value);
        Assert.Null(voice.DurationSeconds);
        Assert.Equal(PackagingStatus.Pending, voice.Status);
        Assert.Equal("sources/episode.wav", voice.Audio.SourceStorageKey);
        Assert.Null(voice.Audio.PlaylistStorageKey);
    }

    [Fact]
    public void CompletePackagingSuccess_sets_ready_status_and_duration()
    {
        var voice = ContentFixtures.PendingVoice(PageId.New());

        voice.CompletePackagingSuccess(90.25, "hls/playlist.m3u8", "hls/seg/");

        Assert.Equal(PackagingStatus.Ready, voice.Status);
        Assert.Equal(90.25, voice.DurationSeconds);
        Assert.Equal("hls/playlist.m3u8", voice.Audio.PlaylistStorageKey);
        Assert.Equal("hls/seg/", voice.Audio.SegmentPrefix);
        voice.EnsureReadyForPlayback();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void CompletePackagingSuccess_rejects_non_positive_or_non_finite_duration(double duration)
    {
        var voice = ContentFixtures.PendingVoice(PageId.New());

        Assert.Throws<InvalidDurationException>(() =>
            voice.CompletePackagingSuccess(duration, "hls/playlist.m3u8", "hls/seg/"));
        Assert.Equal(PackagingStatus.Pending, voice.Status);
        Assert.Null(voice.DurationSeconds);
    }

    [Fact]
    public void CompletePackagingFailure_marks_failed_and_clears_duration()
    {
        var voice = ContentFixtures.PendingVoice(PageId.New());

        voice.CompletePackagingFailure();

        Assert.Equal(PackagingStatus.Failed, voice.Status);
        Assert.Null(voice.DurationSeconds);
        Assert.Null(voice.Audio.PlaylistStorageKey);
        Assert.Throws<AudioNotReadyException>(() => voice.EnsureReadyForPlayback());
    }

    [Fact]
    public void Failed_packaging_retries_through_RetryPackagingSuccess()
    {
        var voice = ContentFixtures.FailedVoice(PageId.New());

        Assert.Throws<InvalidPackagingTransitionException>(() =>
            voice.CompletePackagingSuccess(11, "hls/playlist.m3u8", "hls/seg/"));

        voice.RetryPackagingSuccess(11, "hls/playlist.m3u8", "hls/seg/");

        Assert.Equal(PackagingStatus.Ready, voice.Status);
        Assert.Equal(11, voice.DurationSeconds);
    }

    [Fact]
    public void RetryPackagingSuccess_is_rejected_unless_the_voice_has_failed()
    {
        var pending = ContentFixtures.PendingVoice(PageId.New());
        Assert.Throws<InvalidPackagingTransitionException>(() =>
            pending.RetryPackagingSuccess(11, "hls/playlist.m3u8", "hls/seg/"));

        var ready = ContentFixtures.ReadyVoice(PageId.New());
        Assert.Throws<InvalidPackagingTransitionException>(() =>
            ready.RetryPackagingSuccess(11, "hls/other.m3u8", "hls/other/"));
        Assert.Equal(12.5, ready.DurationSeconds);
    }

    [Fact]
    public void CompletePackagingFailure_is_rejected_after_ready()
    {
        var voice = ContentFixtures.ReadyVoice(PageId.New());

        var ex = Assert.Throws<InvalidPackagingTransitionException>(() => voice.CompletePackagingFailure());
        Assert.Equal(PackagingStatus.Ready, ex.From);
        Assert.Equal(PackagingStatus.Failed, ex.To);
        Assert.Equal(PackagingStatus.Ready, voice.Status);
        Assert.Equal(12.5, voice.DurationSeconds);
    }

    [Fact]
    public void UpdateTitle_works_until_the_page_is_archived()
    {
        var voice = ContentFixtures.PendingVoice(PageId.New());

        voice.UpdateTitle(VoiceTitle.Create("Rewritten"), pageIsArchived: false);

        Assert.Equal("Rewritten", voice.Title.Value);
        Assert.Throws<PageArchivedException>(() =>
            voice.UpdateTitle(VoiceTitle.Create("Too late"), pageIsArchived: true));
        Assert.Equal("Rewritten", voice.Title.Value);
    }

    [Fact]
    public void EnsureReadyForPlayback_rejects_pending_audio()
    {
        var voice = ContentFixtures.PendingVoice(PageId.New());

        var ex = Assert.Throws<AudioNotReadyException>(() => voice.EnsureReadyForPlayback());
        Assert.Equal(voice.Id, ex.VoiceId);
        Assert.Equal(PackagingStatus.Pending, ex.PackagingStatus);
    }

    [Fact]
    public void A_ready_voice_lets_the_page_publish()
    {
        var page = ContentFixtures.DraftPage();
        var voice = ContentFixtures.ReadyVoice(page.Id);
        page.AddVoice(voice);

        page.Publish(ContentFixtures.Readiness((voice.Id, voice.Status)));

        Assert.Equal(PageStatus.Published, page.Status);
        Assert.Equal(PackagingStatus.Ready, voice.Status);
        Assert.True(voice.DurationSeconds > 0);
    }
}
