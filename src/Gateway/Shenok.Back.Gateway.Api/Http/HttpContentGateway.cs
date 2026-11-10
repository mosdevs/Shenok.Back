using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Api.Playback;
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

        if (string.IsNullOrWhiteSpace(page.Status))
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
        Guid? waveformId,
        CancellationToken cancellationToken)
    {
        object body = waveformId is Guid id
            ? new ScanPlaybackBody(voiceId, id)
            : new DirectPlaybackBody(voiceId);
        var issued = await Downstream.PostAsync<IssuedPlaylist>(
            Client(),
            "internal/playback",
            body,
            "Content",
            cancellationToken);
        if (string.IsNullOrEmpty(issued.Playlist) || issued.ExpiresUnixSeconds <= 0)
            throw new UnavailableException("Content is unavailable.");

        return issued;
    }

    public Task<OpenedSegment> OpenSegmentAsync(Guid voiceId, string relativePath, CancellationToken cancellationToken) =>
        Downstream.GetSegmentAsync(
            Client(),
            PlaybackPaths.ContentSegment(voiceId, relativePath),
            "Content",
            cancellationToken);

    public Task<ProxiedResponse> CreatePageAsync(
        string? title,
        string? description,
        string? coverImageUrl,
        CancellationToken cancellationToken) =>
        PostJsonAsync("pages", new PageWriteBody(title, description, coverImageUrl), cancellationToken);

    public Task<ProxiedResponse> UpdatePageAsync(
        Guid pageId,
        string? title,
        string? description,
        string? coverImageUrl,
        CancellationToken cancellationToken) =>
        SendAsync(
            client => client.PutAsJsonAsync(
                $"pages/{pageId:D}",
                new PageWriteBody(title, description, coverImageUrl),
                DownstreamJson.Options,
                cancellationToken),
            cancellationToken);

    public Task<ProxiedResponse> PublishPageAsync(Guid pageId, CancellationToken cancellationToken) =>
        PostEmptyAsync($"pages/{pageId:D}/publish", cancellationToken);

    public Task<ProxiedResponse> ArchivePageAsync(Guid pageId, CancellationToken cancellationToken) =>
        PostEmptyAsync($"pages/{pageId:D}/archive", cancellationToken);

    public Task<ProxiedResponse> ReorderVoicesAsync(
        Guid pageId,
        IReadOnlyList<Guid>? voiceIds,
        CancellationToken cancellationToken) =>
        SendAsync(
            client => client.PutAsJsonAsync(
                $"pages/{pageId:D}/voices/order",
                new ReorderBody(voiceIds ?? []),
                DownstreamJson.Options,
                cancellationToken),
            cancellationToken);

    public Task<ProxiedResponse> AddVoiceAsync(Guid pageId, Guid voiceId, CancellationToken cancellationToken) =>
        PostEmptyAsync($"pages/{pageId:D}/voices/{voiceId:D}", cancellationToken);

    public async Task<ProxiedResponse> UploadVoiceAsync(
        Guid pageId,
        string? title,
        string? contentType,
        long sizeBytes,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var form = new MultipartFormDataContent();
        if (title is not null)
            form.Add(new StringContent(title), "Title");

        var file = new StreamContent(content);
        if (sizeBytes >= 0)
            file.Headers.ContentLength = sizeBytes;
        if (MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
            file.Headers.ContentType = mediaType;

        form.Add(file, "File", "audio");
        return await SendAsync(
            client => client.PostAsync($"pages/{pageId:D}/voices", form, cancellationToken),
            cancellationToken);
    }

    public Task<ProxiedResponse> UpdateVoiceTitleAsync(Guid voiceId, string? title, CancellationToken cancellationToken) =>
        SendAsync(
            client => client.PutAsJsonAsync(
                $"voices/{voiceId:D}",
                new VoiceTitleBody(title),
                DownstreamJson.Options,
                cancellationToken),
            cancellationToken);

    public Task<ProxiedResponse> RegisterWaveformAsync(
        string? targetType,
        Guid targetId,
        CancellationToken cancellationToken) =>
        PostJsonAsync("waveforms", new WaveformBody(targetType, targetId), cancellationToken);

    public Task<ProxiedResponse> RevokeWaveformAsync(Guid waveformId, CancellationToken cancellationToken) =>
        PostEmptyAsync($"waveforms/{waveformId:D}/revoke", cancellationToken);

    private Task<ProxiedResponse> PostJsonAsync(string path, object body, CancellationToken cancellationToken) =>
        SendAsync(
            client => client.PostAsJsonAsync(path, body, DownstreamJson.Options, cancellationToken),
            cancellationToken);

    private Task<ProxiedResponse> PostEmptyAsync(string path, CancellationToken cancellationToken) =>
        SendAsync(client => client.PostAsync(path, content: null, cancellationToken), cancellationToken);

    private Task<ProxiedResponse> SendAsync(
        Func<HttpClient, Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken) =>
        Downstream.ExchangeAsync(() => send(Client()), "Content", cancellationToken);

    private HttpClient Client() => factory.CreateClient(GatewayClients.Content);

    private sealed record ScanPlaybackBody(Guid VoiceId, Guid WaveformId);

    private sealed record DirectPlaybackBody(Guid VoiceId);

    private sealed record PageWriteBody(string? Title, string? Description, string? CoverImageUrl);

    private sealed record ReorderBody(IReadOnlyList<Guid> VoiceIds);

    private sealed record VoiceTitleBody(string? Title);

    private sealed record WaveformBody(string? TargetType, Guid TargetId);
}
