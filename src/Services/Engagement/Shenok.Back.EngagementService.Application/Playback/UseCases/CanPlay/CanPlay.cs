using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Application.Playback.UseCases.CanPlay;

public sealed record CanPlayRequest(Guid UserId, Guid PageId);

public sealed record CanPlayResponse(bool CanPlay);

public sealed class CanPlayValidator : AbstractValidator<CanPlayRequest>
{
    public CanPlayValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PageId).NotEmpty();
    }
}

/// <summary>
/// True only when this user has unlocked the page. Owner playback is the Gateway's decision.
/// </summary>
public sealed class CanPlayUseCase(IUserPageRelationshipRepository relationships)
    : IUseCase<CanPlayRequest, CanPlayResponse>
{
    public async Task<CanPlayResponse> ExecuteAsync(CanPlayRequest request, CancellationToken cancellationToken)
    {
        var relationship = await relationships.GetAsync(
            UserId.From(request.UserId),
            PageId.From(request.PageId),
            cancellationToken);

        return new CanPlayResponse(relationship?.UnlockedAt is not null);
    }
}
