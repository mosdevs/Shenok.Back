using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Http;

internal sealed class HttpContentGateway(IHttpClientFactory factory) : IContentGateway
{
    public async Task<CatalogPage> GetPageAsync(Guid pageId, CancellationToken cancellationToken)
    {
        var page = await Downstream.GetAsync<CatalogPage>(
            Client(),
            $"internal/pages/{pageId:D}",
            "Content",
            cancellationToken);
        if (page.PageId != pageId || page.OwnerId == Guid.Empty || page.Voices is null || string.IsNullOrWhiteSpace(page.Title))
            throw new UnavailableException("Content is unavailable.");

        for (var i = 0; i < page.Voices.Count; i++)
        {
            if (page.Voices[i].VoiceId == Guid.Empty || string.IsNullOrWhiteSpace(page.Voices[i].Title))
                throw new UnavailableException("Content is unavailable.");
        }

        return page;
    }

    public async Task<CatalogVoiceDetail> GetVoiceAsync(Guid voiceId, CancellationToken cancellationToken)
    {
        var voice = await Downstream.GetAsync<CatalogVoiceDetail>(
            Client(),
            $"internal/voices/{voiceId:D}",
            "Content",
            cancellationToken);
        if (voice.VoiceId != voiceId || voice.PageId == Guid.Empty || string.IsNullOrWhiteSpace(voice.Title))
            throw new UnavailableException("Content is unavailable.");

        return voice;
    }

    public async Task<IssuedPlaylist> IssuePlaylistAsync(
        Guid voiceId,
        Guid waveformId,
        CancellationToken cancellationToken)
    {
        var issued = await Downstream.PostAsync<IssuedPlaylist>(
            Client(),
            "internal/playback",
            new PlaybackBody(voiceId, waveformId),
            "Content",
            cancellationToken);
        if (string.IsNullOrEmpty(issued.Playlist) || issued.ExpiresUnixSeconds <= 0)
            throw new UnavailableException("Content is unavailable.");

        return issued;
    }

    private HttpClient Client() => factory.CreateClient(GatewayClients.Content);

    private sealed record PlaybackBody(Guid VoiceId, Guid WaveformId);
}
