using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Application.Follows.UseCases.FollowPage;

public sealed record FollowPageRequest(Guid UserId, Guid PageId);

public sealed class FollowPageValidator : AbstractValidator<FollowPageRequest>
{
    public FollowPageValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PageId).NotEmpty();
    }
}

/// <summary>
/// Flow C. Follow stays independent of unlock. Engagement applies the subscriber count.
/// </summary>
public sealed class FollowPageUseCase(IEngagementGateway engagement) : IUseCase<FollowPageRequest>
{
    public Task ExecuteAsync(FollowPageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return engagement.FollowAsync(request.PageId, cancellationToken);
    }
}
