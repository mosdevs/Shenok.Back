using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Application.Tests;

internal sealed class ScriptedDecoder(WaveformHit? hit) : IScanDecoder
{
    public int Calls { get; private set; }

    public Task<WaveformHit?> DecodeAsync(ScanImage image, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(hit);
    }
}

internal sealed class FakeContent : IContentGateway
{
    public CatalogPage? Page { get; set; }

    public CatalogVoiceDetail? Voice { get; set; }

    public IssuedPlaylist Playlist { get; set; } = new("#EXTM3U\n", 1_700_000_000);

    public Exception? PlaylistFailure { get; set; }

    public Action? OnIssue { get; set; }

    public List<Guid> Pages { get; } = [];

    public List<Guid> Voices { get; } = [];

    public List<(Guid VoiceId, Guid? WaveformId)> Playlists { get; } = [];

    public List<string> Segments { get; } = [];

    public List<string> Commands { get; } = [];

    public Task<CatalogPage> GetPageAsync(Guid pageId, CancellationToken cancellationToken)
    {
        Pages.Add(pageId);
        if (Page is null || Page.PageId != pageId)
            throw new NotFoundException("Page was not found.");

        return Task.FromResult(Page);
    }

    public Task<CatalogVoiceDetail> GetVoiceAsync(Guid voiceId, CancellationToken cancellationToken)
    {
        Voices.Add(voiceId);
        if (Voice is null || Voice.VoiceId != voiceId)
            throw new NotFoundException("Voice was not found.");

        return Task.FromResult(Voice);
    }

    public Task<IssuedPlaylist> IssuePlaylistAsync(Guid voiceId, Guid? waveformId, CancellationToken cancellationToken)
    {
        OnIssue?.Invoke();
        if (PlaylistFailure is not null)
            throw PlaylistFailure;

        Playlists.Add((voiceId, waveformId));
        return Task.FromResult(Playlist);
    }

    public Task<OpenedSegment> OpenSegmentAsync(Guid voiceId, string relativePath, CancellationToken cancellationToken)
    {
        Segments.Add(relativePath);
        var bytes = new byte[] { 9, 8, 7 };
        return Task.FromResult(new OpenedSegment(new MemoryStream(bytes, writable: false), bytes.Length));
    }

    public Task<ProxiedResponse> CreatePageAsync(
        string? title,
        string? description,
        string? coverImageUrl,
        CancellationToken cancellationToken) =>
        Record("create-page");

    public Task<ProxiedResponse> UpdatePageAsync(
        Guid pageId,
        string? title,
        string? description,
        string? coverImageUrl,
        CancellationToken cancellationToken) =>
        Record("update-page");

    public Task<ProxiedResponse> PublishPageAsync(Guid pageId, CancellationToken cancellationToken) =>
        Record("publish-page");

    public Task<ProxiedResponse> ArchivePageAsync(Guid pageId, CancellationToken cancellationToken) =>
        Record("archive-page");

    public Task<ProxiedResponse> ReorderVoicesAsync(
        Guid pageId,
        IReadOnlyList<Guid>? voiceIds,
        CancellationToken cancellationToken) =>
        Record("reorder");

    public Task<ProxiedResponse> AddVoiceAsync(Guid pageId, Guid voiceId, CancellationToken cancellationToken) =>
        Record("add-voice");

    public Task<ProxiedResponse> UploadVoiceAsync(
        Guid pageId,
        string? title,
        string? contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken) =>
        Record("upload");

    public Task<ProxiedResponse> UpdateVoiceTitleAsync(Guid voiceId, string? title, CancellationToken cancellationToken) =>
        Record("update-voice");

    public Task<ProxiedResponse> RegisterWaveformAsync(
        string? targetType,
        Guid targetId,
        CancellationToken cancellationToken) =>
        Record("register-waveform");

    public Task<ProxiedResponse> RevokeWaveformAsync(Guid waveformId, CancellationToken cancellationToken) =>
        Record("revoke-waveform");

    private Task<ProxiedResponse> Record(string command)
    {
        Commands.Add(command);
        return Task.FromResult(new ProxiedResponse(204, null, []));
    }
}

internal sealed class FakeEngagement : IEngagementGateway
{
    public ScanUnlock Unlock { get; set; } = new(PromptFollow: true, ResumePositionSeconds: 0);

    public bool Unlocked { get; set; }

    public bool Following { get; set; }

    public List<ListeningVoice> Listening { get; } = [];

    public List<(string ScanId, Guid WaveformId, Guid VoiceId, Guid PageId)> Scans { get; } = [];

    public int Unlocks { get; private set; }

    private readonly HashSet<string> _receipts = new(StringComparer.Ordinal);

    public List<Guid> Follows { get; } = [];

    public List<Guid> Unfollows { get; } = [];

    public List<Guid> CanPlayPages { get; } = [];

    public List<(Guid VoiceId, string SessionId, int Position, bool Completed, bool IsSeek)> Progress { get; } = [];

    public Task<ScanUnlock> RegisterScanAsync(
        string scanId,
        Guid waveformId,
        Guid voiceId,
        Guid pageId,
        CancellationToken cancellationToken)
    {
        Scans.Add((scanId, waveformId, voiceId, pageId));
        if (_receipts.Add(scanId))
            Unlocks++;

        return Task.FromResult(Unlock);
    }

    public Task FollowAsync(Guid pageId, CancellationToken cancellationToken)
    {
        Follows.Add(pageId);
        return Task.CompletedTask;
    }

    public Task UnfollowAsync(Guid pageId, CancellationToken cancellationToken)
    {
        Unfollows.Add(pageId);
        return Task.CompletedTask;
    }

    public Task<ListeningSnapshot> GetListeningStateAsync(
        Guid pageId,
        IReadOnlyList<Guid> voiceIds,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ListeningSnapshot(Following, Unlocked, Listening));

    public Task<bool> CanPlayAsync(Guid pageId, CancellationToken cancellationToken)
    {
        CanPlayPages.Add(pageId);
        return Task.FromResult(Unlocked);
    }

    public Task ReportProgressAsync(
        Guid voiceId,
        string playSessionId,
        int positionSeconds,
        bool completed,
        bool isSeek,
        CancellationToken cancellationToken)
    {
        Progress.Add((voiceId, playSessionId, positionSeconds, completed, isSeek));
        return Task.CompletedTask;
    }
}

internal sealed class FakeLinks : IPlaybackLink
{
    public string PlaylistUrl(Guid voiceId, Guid waveformId, long expiresUnixSeconds) =>
        $"https://playback.test/audio/{voiceId:D}/playlist.m3u8?waveformId={waveformId:D}&exp={expiresUnixSeconds}&sig=test";

    public string PlaylistUrl(Guid voiceId, long expiresUnixSeconds) =>
        $"https://playback.test/audio/{voiceId:D}/playlist.m3u8?exp={expiresUnixSeconds}&sig=test";

    public bool SignatureMatches(Guid voiceId, Guid waveformId, long expiresUnixSeconds, string signature) =>
        signature == "test" && waveformId != Guid.Empty;

    public bool SignatureMatches(Guid voiceId, long expiresUnixSeconds, string signature) =>
        signature == "test";

    public bool SegmentSignatureMatches(Guid voiceId, string relativePath, long expiresUnixSeconds, string signature) =>
        signature == "test" && voiceId != Guid.Empty && !string.IsNullOrWhiteSpace(relativePath);
}

internal sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
