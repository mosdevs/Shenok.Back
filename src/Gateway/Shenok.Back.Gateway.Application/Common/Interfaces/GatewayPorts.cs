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
    IReadOnlyList<CatalogVoice> Voices,
    string? Status = null);

public sealed record CatalogVoiceDetail(
    Guid VoiceId,
    Guid PageId,
    string Title,
    double? DurationSeconds,
    string? PackagingStatus);

public sealed record IssuedPlaylist(string Playlist, long ExpiresUnixSeconds);

/// <summary>
/// A downstream HTTP result copied through the Gateway. The body is the raw payload.
/// <see cref="Location"/> is a relative path, never an internal host.
/// </summary>
public sealed record ProxiedResponse(int StatusCode, string? ContentType, byte[] Body, string? Location = null);

/// <summary>
/// One private media segment. The caller owns <see cref="Content"/> and must dispose it.
/// </summary>
public sealed record OpenedSegment(Stream Content, long Length);

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

    Task<IssuedPlaylist> IssuePlaylistAsync(Guid voiceId, Guid? waveformId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens one private segment after the Gateway has checked the signature.
    /// The stream is Content's object. This port does not copy it into a buffer.
    /// </summary>
    Task<OpenedSegment> OpenSegmentAsync(Guid voiceId, string relativePath, CancellationToken cancellationToken);

    Task<ProxiedResponse> CreatePageAsync(
        string? title,
        string? description,
        string? coverImageUrl,
        CancellationToken cancellationToken);

    Task<ProxiedResponse> UpdatePageAsync(
        Guid pageId,
        string? title,
        string? description,
        string? coverImageUrl,
        CancellationToken cancellationToken);

    Task<ProxiedResponse> PublishPageAsync(Guid pageId, CancellationToken cancellationToken);

    Task<ProxiedResponse> ArchivePageAsync(Guid pageId, CancellationToken cancellationToken);

    Task<ProxiedResponse> ReorderVoicesAsync(
        Guid pageId,
        IReadOnlyList<Guid>? voiceIds,
        CancellationToken cancellationToken);

    Task<ProxiedResponse> AddVoiceAsync(Guid pageId, Guid voiceId, CancellationToken cancellationToken);

    Task<ProxiedResponse> UploadVoiceAsync(
        Guid pageId,
        string? title,
        string? contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken);

    Task<ProxiedResponse> UpdateVoiceTitleAsync(Guid voiceId, string? title, CancellationToken cancellationToken);

    Task<ProxiedResponse> RegisterWaveformAsync(
        string? targetType,
        Guid targetId,
        CancellationToken cancellationToken);

    Task<ProxiedResponse> RevokeWaveformAsync(Guid waveformId, CancellationToken cancellationToken);
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
/// Builds and checks the short-lived playlist and segment URLs.
/// Content chooses the expiry when it issues the playlist. This port only signs that instant.
/// The scan URL carries a waveform id. A page-list play signs the same playlist path without one.
/// </summary>
public interface IPlaybackLink
{
    string PlaylistUrl(Guid voiceId, Guid waveformId, long expiresUnixSeconds);

    string PlaylistUrl(Guid voiceId, long expiresUnixSeconds);

    bool SignatureMatches(Guid voiceId, Guid waveformId, long expiresUnixSeconds, string signature);

    bool SignatureMatches(Guid voiceId, long expiresUnixSeconds, string signature);

    bool SegmentSignatureMatches(Guid voiceId, string relativePath, long expiresUnixSeconds, string signature);
}
