using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Application.Playback.UseCases.OpenPlaylist;

public sealed record OpenPlaylistRequest(Guid VoiceId, Guid? WaveformId, long ExpiresUnixSeconds, string Signature);

public sealed record OpenPlaylistResponse(string Playlist);

/// <summary>
/// Returns the signed playlist body. The signature is checked before Content is called.
/// A scan URL carries a waveform id. A page-list URL signs the same path without one.
/// Either form asks Content to issue the playlist again so the segment signatures are fresh.
/// The media player does not send the caller's access token, so the link itself is the credential.
/// </summary>
public sealed class OpenPlaylistUseCase(IPlaybackLink links, IContentGateway content, TimeProvider clock)
    : IUseCase<OpenPlaylistRequest, OpenPlaylistResponse>
{
    public async Task<OpenPlaylistResponse> ExecuteAsync(
        OpenPlaylistRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var waveformId = AcceptedWaveform(request);
        var expired = request.ExpiresUnixSeconds <= clock.GetUtcNow().ToUnixTimeSeconds();
        var signature = request.Signature ?? string.Empty;
        var signed = !expired
            && request.VoiceId != Guid.Empty
            && SignatureMatches(request.VoiceId, waveformId, request.ExpiresUnixSeconds, signature);
        if (!signed)
            throw new ForbiddenException(GatewayLimits.PlaylistLinkDetail);

        var issued = await content.IssuePlaylistAsync(request.VoiceId, waveformId, cancellationToken);
        return new OpenPlaylistResponse(issued.Playlist);
    }

    /// <summary>
    /// A missing waveform is the page-list form. An empty guid is neither form.
    /// </summary>
    private static Guid? AcceptedWaveform(OpenPlaylistRequest request) =>
        request.WaveformId is Guid waveformId && waveformId != Guid.Empty ? waveformId : request.WaveformId;

    private bool SignatureMatches(Guid voiceId, Guid? waveformId, long expiresUnixSeconds, string signature)
    {
        if (waveformId is Guid id && id != Guid.Empty)
            return links.SignatureMatches(voiceId, id, expiresUnixSeconds, signature);

        if (waveformId is null)
            return links.SignatureMatches(voiceId, expiresUnixSeconds, signature);

        return false;
    }
}
