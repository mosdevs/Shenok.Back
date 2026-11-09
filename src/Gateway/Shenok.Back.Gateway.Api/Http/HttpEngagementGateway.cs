using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Http;

internal sealed class HttpEngagementGateway(IHttpClientFactory factory) : IEngagementGateway
{
    public async Task<ScanUnlock> RegisterScanAsync(
        string scanId,
        Guid waveformId,
        Guid voiceId,
        Guid pageId,
        CancellationToken cancellationToken)
    {
        var unlock = await Downstream.PostAsync<ScanPayload>(
            Client(),
            "internal/scans",
            new ScanBody(scanId, waveformId, voiceId, pageId),
            "Engagement",
            cancellationToken);
        if (unlock.ResumePositionSeconds < 0)
            throw new UnavailableException("Engagement is unavailable.");

        return new ScanUnlock(unlock.PromptFollow, unlock.ResumePositionSeconds);
    }

    public Task FollowAsync(Guid pageId, CancellationToken cancellationToken) =>
        Downstream.PostAsync(Client(), $"internal/pages/{pageId:D}/follow", new { }, "Engagement", cancellationToken);

    public Task UnfollowAsync(Guid pageId, CancellationToken cancellationToken) =>
        Downstream.DeleteAsync(Client(), $"internal/pages/{pageId:D}/follow", "Engagement", cancellationToken);

    public async Task<ListeningSnapshot> GetListeningStateAsync(
        Guid pageId,
        IReadOnlyList<Guid> voiceIds,
        CancellationToken cancellationToken)
    {
        var snapshot = await Downstream.PostAsync<ListeningPayload>(
            Client(),
            "internal/listening-state",
            new ListeningBody(pageId, voiceIds),
            "Engagement",
            cancellationToken);
        if (snapshot.Voices is null)
            throw new UnavailableException("Engagement is unavailable.");

        return new ListeningSnapshot(snapshot.IsFollowing, snapshot.Unlocked, snapshot.Voices);
    }

    public async Task<bool> CanPlayAsync(Guid pageId, CancellationToken cancellationToken)
    {
        var playback = await Downstream.PostAsync<CanPlayPayload>(
            Client(),
            "internal/can-play",
            new PageBody(pageId),
            "Engagement",
            cancellationToken);
        return playback.CanPlay;
    }

    public Task ReportProgressAsync(
        Guid voiceId,
        string playSessionId,
        int positionSeconds,
        bool completed,
        bool isSeek,
        CancellationToken cancellationToken) =>
        Downstream.PostAsync(
            Client(),
            "internal/progress",
            new ProgressBody(voiceId, playSessionId, positionSeconds, completed, isSeek),
            "Engagement",
            cancellationToken);

    private HttpClient Client() => factory.CreateClient(GatewayClients.Engagement);

    private sealed record ScanBody(string ScanId, Guid WaveformId, Guid VoiceId, Guid PageId);

    private sealed record ScanPayload(bool PromptFollow, int ResumePositionSeconds);

    private sealed record ListeningBody(Guid PageId, IReadOnlyList<Guid> VoiceIds);

    private sealed record ListeningPayload(bool IsFollowing, bool Unlocked, IReadOnlyList<ListeningVoice> Voices);

    private sealed record PageBody(Guid PageId);

    private sealed record CanPlayPayload(bool CanPlay);

    private sealed record ProgressBody(
        Guid VoiceId,
        string PlaySessionId,
        int PositionSeconds,
        bool Completed,
        bool IsSeek);
}
