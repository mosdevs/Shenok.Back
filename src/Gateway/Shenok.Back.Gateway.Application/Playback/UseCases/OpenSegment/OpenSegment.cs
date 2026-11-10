using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Application.Playback.UseCases.OpenSegment;

public sealed record OpenSegmentRequest(Guid VoiceId, string? RelativePath, long ExpiresUnixSeconds, string Signature);

/// <summary>
/// Streams one HLS segment. The signature is checked before Content is called.
/// The player does not send an access token. The link is the credential, and it dies at <c>exp</c>.
/// </summary>
public sealed class OpenSegmentUseCase(IPlaybackLink links, IContentGateway content, TimeProvider clock)
    : IUseCase<OpenSegmentRequest, OpenedSegment>
{
    public Task<OpenedSegment> ExecuteAsync(OpenSegmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var expired = request.ExpiresUnixSeconds <= clock.GetUtcNow().ToUnixTimeSeconds();
        var signed = !expired
            && links.SegmentSignatureMatches(
                request.VoiceId,
                request.RelativePath ?? string.Empty,
                request.ExpiresUnixSeconds,
                request.Signature ?? string.Empty);
        if (!signed)
            throw new ForbiddenException(GatewayLimits.PlaylistLinkDetail);

        return content.OpenSegmentAsync(request.VoiceId, request.RelativePath ?? string.Empty, cancellationToken);
    }
}
