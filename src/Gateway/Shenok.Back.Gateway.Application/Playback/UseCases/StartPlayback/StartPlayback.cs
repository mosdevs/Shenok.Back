using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Application.Playback.UseCases.StartPlayback;

public sealed record StartPlaybackRequest(Guid UserId, Guid VoiceId);

public sealed record StartPlaybackResponse(string AudioUrl, double? Duration, long ExpiresUnixSeconds);

public sealed class StartPlaybackValidator : AbstractValidator<StartPlaybackRequest>
{
    public StartPlaybackValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.VoiceId).NotEmpty();
    }
}

/// <summary>
/// Flow E playback. The owner, or a listener who can already play, receives a playlist URL with no waveform.
/// Anyone else is rejected before Content is asked to issue the playlist.
/// </summary>
public sealed class StartPlaybackUseCase(IContentGateway content, IEngagementGateway engagement, IPlaybackLink links)
    : IUseCase<StartPlaybackRequest, StartPlaybackResponse>
{
    public async Task<StartPlaybackResponse> ExecuteAsync(
        StartPlaybackRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var voice = await content.GetVoiceAsync(request.VoiceId, cancellationToken);
        var page = await content.GetPageAsync(voice.PageId, cancellationToken);
        if (request.UserId != page.OwnerId)
        {
            var canPlay = await engagement.CanPlayAsync(page.PageId, cancellationToken);
            if (!canPlay)
                throw new ForbiddenException(GatewayLimits.LockedDetail);
        }

        var issued = await content.IssuePlaylistAsync(voice.VoiceId, waveformId: null, cancellationToken);
        return new StartPlaybackResponse(
            links.PlaylistUrl(voice.VoiceId, issued.ExpiresUnixSeconds),
            voice.DurationSeconds,
            issued.ExpiresUnixSeconds);
    }
}
