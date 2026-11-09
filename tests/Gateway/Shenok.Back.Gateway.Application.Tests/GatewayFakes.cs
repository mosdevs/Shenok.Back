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

    public List<Guid> Pages { get; } = [];

    public List<Guid> Voices { get; } = [];

    public List<(Guid VoiceId, Guid WaveformId)> Playlists { get; } = [];

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

    public Task<IssuedPlaylist> IssuePlaylistAsync(Guid voiceId, Guid waveformId, CancellationToken cancellationToken)
    {
        Playlists.Add((voiceId, waveformId));
        return Task.FromResult(Playlist);
    }
}

internal sealed class FakeEngagement : IEngagementGateway
{
    public ScanUnlock Unlock { get; set; } = new(PromptFollow: true, ResumePositionSeconds: 0);

    public bool Unlocked { get; set; }

    public bool Following { get; set; }

    public List<ListeningVoice> Listening { get; } = [];

    public List<(string ScanId, Guid WaveformId, Guid VoiceId, Guid PageId)> Scans { get; } = [];

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

    public bool SignatureMatches(Guid voiceId, Guid waveformId, long expiresUnixSeconds, string signature) =>
        signature == "test";
}

internal sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
