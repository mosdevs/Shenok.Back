using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Domain.Tests;

public class AudioAssetTests
{
    [Fact]
    public void PendingUpload_stores_source_fields_and_starts_pending()
    {
        var asset = AudioAsset.PendingUpload("  sources/a.wav  ", "  audio/wav  ", 1);

        Assert.Equal("sources/a.wav", asset.SourceStorageKey);
        Assert.Equal("audio/wav", asset.SourceContentType);
        Assert.Equal(1, asset.SizeBytes);
        Assert.Equal(PackagingStatus.Pending, asset.PackagingStatus);
        Assert.Null(asset.PlaylistStorageKey);
        Assert.Null(asset.SegmentPrefix);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PendingUpload_rejects_a_non_positive_size(long sizeBytes)
    {
        Assert.Throws<InvalidAudioAssetException>(() =>
            AudioAsset.PendingUpload("sources/a.wav", "audio/wav", sizeBytes));
    }

    [Fact]
    public void PendingUpload_rejects_a_size_over_100_mb()
    {
        Assert.Throws<InvalidAudioAssetException>(() =>
            AudioAsset.PendingUpload("sources/a.wav", "audio/wav", AudioAsset.MaxSourceSizeBytes + 1));
        var atLimit = AudioAsset.PendingUpload("sources/a.wav", "audio/wav", AudioAsset.MaxSourceSizeBytes);
        Assert.Equal(AudioAsset.MaxSourceSizeBytes, atLimit.SizeBytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PendingUpload_rejects_a_blank_source_key(string? key)
    {
        Assert.Throws<InvalidAudioAssetException>(() =>
            AudioAsset.PendingUpload(key!, "audio/wav", 10));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PendingUpload_rejects_a_blank_content_type(string? contentType)
    {
        Assert.Throws<InvalidAudioAssetException>(() =>
            AudioAsset.PendingUpload("sources/a.wav", contentType!, 10));
    }

    [Fact]
    public void PendingUpload_rejects_keys_and_content_types_that_are_too_long()
    {
        Assert.Throws<InvalidAudioAssetException>(() =>
            AudioAsset.PendingUpload(new string('k', AudioAsset.MaxKeyLength + 1), "audio/wav", 10));
        Assert.Throws<InvalidAudioAssetException>(() =>
            AudioAsset.PendingUpload("sources/a.wav", new string('t', AudioAsset.MaxContentTypeLength + 1), 10));
    }

    [Fact]
    public void MarkPackagingReady_requires_playlist_and_segment_fields()
    {
        var asset = AudioAsset.PendingUpload("sources/a.wav", "audio/wav", 10);

        asset.MarkPackagingReady("  hls/playlist.m3u8  ", "  hls/seg/  ");

        Assert.Equal(PackagingStatus.Ready, asset.PackagingStatus);
        Assert.Equal("hls/playlist.m3u8", asset.PlaylistStorageKey);
        Assert.Equal("hls/seg/", asset.SegmentPrefix);
    }

    [Theory]
    [InlineData("", "hls/seg/")]
    [InlineData("hls/playlist.m3u8", "")]
    public void MarkPackagingReady_rejects_blank_package_keys(string playlist, string prefix)
    {
        var asset = AudioAsset.PendingUpload("sources/a.wav", "audio/wav", 10);

        Assert.Throws<InvalidAudioAssetException>(() => asset.MarkPackagingReady(playlist, prefix));
        Assert.Equal(PackagingStatus.Pending, asset.PackagingStatus);
    }

    [Fact]
    public void MarkPackagingReady_cannot_run_twice()
    {
        var asset = AudioAsset.PendingUpload("sources/a.wav", "audio/wav", 10);
        asset.MarkPackagingReady("hls/playlist.m3u8", "hls/seg/");

        var ex = Assert.Throws<InvalidPackagingTransitionException>(() =>
            asset.MarkPackagingReady("hls/other.m3u8", "hls/other/"));
        Assert.Equal(PackagingStatus.Ready, ex.From);
        Assert.Equal(PackagingStatus.Ready, ex.To);
        Assert.Equal("hls/playlist.m3u8", asset.PlaylistStorageKey);
    }

    [Fact]
    public void MarkPackagingFailed_is_allowed_only_from_pending()
    {
        var pending = AudioAsset.PendingUpload("sources/a.wav", "audio/wav", 10);
        pending.MarkPackagingFailed();
        Assert.Equal(PackagingStatus.Failed, pending.PackagingStatus);
        Assert.Null(pending.PlaylistStorageKey);

        var ready = AudioAsset.PendingUpload("sources/b.wav", "audio/wav", 10);
        ready.MarkPackagingReady("hls/playlist.m3u8", "hls/seg/");
        Assert.Throws<InvalidPackagingTransitionException>(() => ready.MarkPackagingFailed());

        var failed = AudioAsset.PendingUpload("sources/c.wav", "audio/wav", 10);
        failed.MarkPackagingFailed();
        Assert.Throws<InvalidPackagingTransitionException>(() => failed.MarkPackagingFailed());
    }

    [Fact]
    public void Failed_asset_retries_through_RetryPackagingReady()
    {
        var asset = AudioAsset.PendingUpload("sources/a.wav", "audio/wav", 10);
        asset.MarkPackagingFailed();

        Assert.Throws<InvalidPackagingTransitionException>(() =>
            asset.MarkPackagingReady("hls/playlist.m3u8", "hls/seg/"));

        asset.RetryPackagingReady("hls/playlist.m3u8", "hls/seg/");

        Assert.Equal(PackagingStatus.Ready, asset.PackagingStatus);
        Assert.Equal("hls/playlist.m3u8", asset.PlaylistStorageKey);
    }

    [Fact]
    public void RetryPackagingReady_is_rejected_unless_the_asset_has_failed()
    {
        var pending = AudioAsset.PendingUpload("sources/a.wav", "audio/wav", 10);
        Assert.Throws<InvalidPackagingTransitionException>(() =>
            pending.RetryPackagingReady("hls/playlist.m3u8", "hls/seg/"));

        var ready = AudioAsset.PendingUpload("sources/b.wav", "audio/wav", 10);
        ready.MarkPackagingReady("hls/playlist.m3u8", "hls/seg/");
        Assert.Throws<InvalidPackagingTransitionException>(() =>
            ready.RetryPackagingReady("hls/other.m3u8", "hls/other/"));
        Assert.Equal("hls/playlist.m3u8", ready.PlaylistStorageKey);
    }
}
