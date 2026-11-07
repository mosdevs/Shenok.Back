using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Exceptions;
using Shenok.Back.SharedKernel;

namespace Shenok.Back.ContentService.Domain.Aggregates.Voices;

/// <summary>
/// One episode. <see cref="Status"/> is the packaging status; playback uses that same value.
/// Play count lives on <c>VoiceCounter</c> and is never changed here.
/// The client cannot set <see cref="DurationSeconds"/>; only a successful package writes it.
/// </summary>
public sealed class Voice : AggregateRoot<VoiceId>
{
    public PageId PageId { get; private set; } = null!;
    public VoiceTitle Title { get; private set; } = null!;
    public double? DurationSeconds { get; private set; }
    public AudioAsset Audio { get; private set; } = null!;
    public PackagingStatus Status => Audio.PackagingStatus;

    private Voice()
    {
    }

    private Voice(VoiceId id, PageId pageId, VoiceTitle title, AudioAsset audio) : base(id)
    {
        PageId = pageId;
        Title = title;
        Audio = audio;
    }

    public static Voice CreatePending(
        PageId pageId,
        VoiceTitle title,
        string sourceStorageKey,
        string sourceContentType,
        long sizeBytes)
    {
        ArgumentNullException.ThrowIfNull(pageId);
        ArgumentNullException.ThrowIfNull(title);

        return new Voice(
            VoiceId.New(),
            pageId,
            title,
            AudioAsset.PendingUpload(sourceStorageKey, sourceContentType, sizeBytes));
    }

    public void UpdateTitle(VoiceTitle title, bool pageIsArchived)
    {
        if (pageIsArchived)
            throw new PageArchivedException(PageId);

        ArgumentNullException.ThrowIfNull(title);
        Title = title;
    }

    public void CompletePackagingSuccess(double durationSeconds, string playlistStorageKey, string segmentPrefix)
    {
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0)
            throw new InvalidDurationException();

        Audio.MarkPackagingReady(playlistStorageKey, segmentPrefix);
        DurationSeconds = durationSeconds;
    }

    public void CompletePackagingFailure()
    {
        Audio.MarkPackagingFailed();
        DurationSeconds = null;
    }

    public void EnsureReadyForPlayback()
    {
        if (Status != PackagingStatus.Ready
            || DurationSeconds is not > 0
            || string.IsNullOrWhiteSpace(Audio.PlaylistStorageKey)
            || string.IsNullOrWhiteSpace(Audio.SegmentPrefix))
            throw new AudioNotReadyException(Id, Status);
    }
}
