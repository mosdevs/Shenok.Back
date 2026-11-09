using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Application.Playback.UseCases.OpenPlaylist;

public sealed record OpenPlaylistRequest(Guid VoiceId, Guid WaveformId, long ExpiresUnixSeconds, string Signature);

public sealed record OpenPlaylistResponse(string Playlist);

/// <summary>
/// Returns the signed playlist body for a scan URL. The signature is checked before Content is called.
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

        var expired = request.ExpiresUnixSeconds <= clock.GetUtcNow().ToUnixTimeSeconds();
        var signed = !expired
            && request.VoiceId != Guid.Empty
            && request.WaveformId != Guid.Empty
            && links.SignatureMatches(
                request.VoiceId,
                request.WaveformId,
                request.ExpiresUnixSeconds,
                request.Signature ?? string.Empty);
        if (!signed)
            throw new ForbiddenException(GatewayLimits.PlaylistLinkDetail);

        var issued = await content.IssuePlaylistAsync(request.VoiceId, request.WaveformId, cancellationToken);
        return new OpenPlaylistResponse(issued.Playlist);
    }
}
