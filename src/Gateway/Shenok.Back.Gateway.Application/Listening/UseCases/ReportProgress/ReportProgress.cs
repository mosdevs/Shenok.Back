using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.Gateway.Application.Common;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Application.Listening.UseCases.ReportProgress;

public sealed record ReportProgressRequest(
    Guid UserId,
    Guid VoiceId,
    string PlaySessionId,
    int PositionSeconds,
    bool Completed,
    bool IsSeek,
    bool IsAdmin);

public sealed class ReportProgressValidator : AbstractValidator<ReportProgressRequest>
{
    public ReportProgressValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.VoiceId).NotEmpty();
        RuleFor(x => x.PlaySessionId)
            .Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= GatewayLimits.ClientTokenLength)
            .WithMessage($"Play session id must be 1 to {GatewayLimits.ClientTokenLength} characters after trim.");
        RuleFor(x => x.PositionSeconds).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// Flow F. The owner or an admin may report progress without an unlock.
/// Everyone else must already be able to play. Engagement still owns completion and seek rules.
/// </summary>
public sealed class ReportProgressUseCase(IContentGateway content, IEngagementGateway engagement)
    : IUseCase<ReportProgressRequest>
{
    public async Task ExecuteAsync(ReportProgressRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var voice = await content.GetVoiceAsync(request.VoiceId, cancellationToken);
        var page = await content.GetPageAsync(voice.PageId, cancellationToken);
        if (request.UserId != page.OwnerId && !request.IsAdmin)
        {
            var canPlay = await engagement.CanPlayAsync(page.PageId, cancellationToken);
            if (!canPlay)
                throw new ForbiddenException(GatewayLimits.LockedDetail);
        }

        await engagement.ReportProgressAsync(
            voice.VoiceId,
            request.PlaySessionId.Trim(),
            request.PositionSeconds,
            request.Completed,
            request.IsSeek,
            cancellationToken);
    }
}
