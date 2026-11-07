using Shenok.Back.ContentService.Domain.Exceptions;
using PackagingState = Shenok.Back.ContentService.Domain.Aggregates.Voices.PackagingStatus;

namespace Shenok.Back.ContentService.Domain.Aggregates.Voices;

/// <summary>
/// Source upload and the HLS package produced from it. Duration is stored on <see cref="Voice"/>, not here.
/// </summary>
public sealed class AudioAsset
{
    public const int MaxKeyLength = 1024;
    public const int MaxContentTypeLength = 255;

    public string SourceStorageKey { get; private set; } = string.Empty;
    public string SourceContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string? PlaylistStorageKey { get; private set; }
    public string? SegmentPrefix { get; private set; }
    public PackagingStatus PackagingStatus { get; private set; }

    private AudioAsset()
    {
    }

    private AudioAsset(string sourceStorageKey, string sourceContentType, long sizeBytes)
    {
        SourceStorageKey = sourceStorageKey;
        SourceContentType = sourceContentType;
        SizeBytes = sizeBytes;
        PackagingStatus = PackagingState.Pending;
    }

    public static AudioAsset PendingUpload(string sourceStorageKey, string sourceContentType, long sizeBytes)
    {
        if (sizeBytes < 0)
            throw new InvalidAudioAssetException("Size cannot be negative.");

        return new AudioAsset(
            RequireKey(sourceStorageKey, "Source storage key"),
            RequireContentType(sourceContentType),
            sizeBytes);
    }

    public void MarkPackagingReady(string playlistStorageKey, string segmentPrefix)
    {
        if (PackagingStatus == PackagingState.Ready)
            throw new InvalidPackagingTransitionException(PackagingStatus, PackagingState.Ready);

        var playlist = RequireKey(playlistStorageKey, "Playlist storage key");
        var prefix = RequireKey(segmentPrefix, "Segment prefix");
        PlaylistStorageKey = playlist;
        SegmentPrefix = prefix;
        PackagingStatus = PackagingState.Ready;
    }

    public void MarkPackagingFailed()
    {
        if (PackagingStatus != PackagingState.Pending)
            throw new InvalidPackagingTransitionException(PackagingStatus, PackagingState.Failed);

        PackagingStatus = PackagingState.Failed;
        PlaylistStorageKey = null;
        SegmentPrefix = null;
    }

    private static string RequireKey(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidAudioAssetException($"{name} is required.");

        var trimmed = value.Trim();
        if (trimmed.Length > MaxKeyLength)
            throw new InvalidAudioAssetException($"{name} must be {MaxKeyLength} characters or fewer.");

        return trimmed;
    }

    private static string RequireContentType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidAudioAssetException("Source content type is required.");

        var trimmed = value.Trim();
        if (trimmed.Length > MaxContentTypeLength)
            throw new InvalidAudioAssetException($"Source content type must be {MaxContentTypeLength} characters or fewer.");

        return trimmed;
    }
}
