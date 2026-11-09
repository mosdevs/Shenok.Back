using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Application.Follows.UseCases.UnfollowPage;

public sealed record UnfollowPageRequest(Guid UserId, Guid PageId);

public sealed class UnfollowPageValidator : AbstractValidator<UnfollowPageRequest>
{
    public UnfollowPageValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PageId).NotEmpty();
    }
}

/// <summary>
/// Flow C. Clears the follow. Engagement applies the subscriber delta and keeps the floor at zero.
/// </summary>
public sealed class UnfollowPageUseCase(IEngagementGateway engagement) : IUseCase<UnfollowPageRequest>
{
    public Task ExecuteAsync(UnfollowPageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return engagement.UnfollowAsync(request.PageId, cancellationToken);
    }
}
