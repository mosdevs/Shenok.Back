namespace Shenok.Back.Gateway.Application.Common.Interfaces;

public sealed record ScanImage(string ContentType, byte[] Image);

public sealed record WaveformHit(Guid WaveformId, string TargetType, Guid TargetId);

public sealed record CatalogVoice(
    Guid VoiceId,
    string Title,
    double? DurationSeconds,
    string? PackagingStatus);

public sealed record CatalogPage(
    Guid PageId,
    Guid OwnerId,
    string Title,
    string? CoverImageUrl,
    long SubscriberCount,
    IReadOnlyList<CatalogVoice> Voices);

public sealed record CatalogVoiceDetail(
    Guid VoiceId,
    Guid PageId,
    string Title,
    double? DurationSeconds,
    string? PackagingStatus);

public sealed record IssuedPlaylist(string Playlist, long ExpiresUnixSeconds);

public sealed record ScanUnlock(bool PromptFollow, int ResumePositionSeconds);

public sealed record ListeningVoice(Guid VoiceId, bool IsCompleted, int PositionSeconds);

public sealed record ListeningSnapshot(
    bool IsFollowing,
    bool Unlocked,
    IReadOnlyList<ListeningVoice> Voices);

/// <summary>
/// Image decode. A null result is a miss and must not unlock anything.
/// </summary>
public interface IScanDecoder
{
    Task<WaveformHit?> DecodeAsync(ScanImage image, CancellationToken cancellationToken);
}

public interface IContentGateway
{
    Task<CatalogPage> GetPageAsync(Guid pageId, CancellationToken cancellationToken);

    Task<CatalogVoiceDetail> GetVoiceAsync(Guid voiceId, CancellationToken cancellationToken);

    Task<IssuedPlaylist> IssuePlaylistAsync(Guid voiceId, Guid waveformId, CancellationToken cancellationToken);
}

public interface IEngagementGateway
{
    Task<ScanUnlock> RegisterScanAsync(
        string scanId,
        Guid waveformId,
        Guid voiceId,
        Guid pageId,
        CancellationToken cancellationToken);

    Task FollowAsync(Guid pageId, CancellationToken cancellationToken);

    Task UnfollowAsync(Guid pageId, CancellationToken cancellationToken);

    Task<ListeningSnapshot> GetListeningStateAsync(
        Guid pageId,
        IReadOnlyList<Guid> voiceIds,
        CancellationToken cancellationToken);

    Task<bool> CanPlayAsync(Guid pageId, CancellationToken cancellationToken);

    Task ReportProgressAsync(
        Guid voiceId,
        string playSessionId,
        int positionSeconds,
        bool completed,
        bool isSeek,
        CancellationToken cancellationToken);
}

/// <summary>
/// Builds and checks the short-lived playlist URL returned by a scan.
/// Content chooses the expiry when it issues the playlist. This port only signs that instant.
/// </summary>
public interface IPlaybackLink
{
    string PlaylistUrl(Guid voiceId, Guid waveformId, long expiresUnixSeconds);

    bool SignatureMatches(Guid voiceId, Guid waveformId, long expiresUnixSeconds, string signature);
}
