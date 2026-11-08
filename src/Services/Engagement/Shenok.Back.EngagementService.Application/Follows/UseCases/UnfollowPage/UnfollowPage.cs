using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Application.Follows.UseCases.UnfollowPage;

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
/// Clears a follow. A missing relationship is success and does not insert a row.
/// </summary>
public sealed class UnfollowPageUseCase(
    IUserPageRelationshipRepository relationships,
    IContentCounters counters,
    IUnitOfWork unitOfWork) : IUseCase<UnfollowPageRequest>
{
    public async Task ExecuteAsync(UnfollowPageRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId.From(request.UserId);
        var pageId = PageId.From(request.PageId);

        for (var attempt = 0; attempt < EngagementPersistence.MaxAttempts; attempt++)
        {
            var isLast = attempt == EngagementPersistence.MaxAttempts - 1;
            var relationship = await relationships.GetAsync(userId, pageId, cancellationToken);
            if (relationship is null)
                return;

            if (!relationship.Unfollow())
                return;

            await relationships.UpdateAsync(relationship, cancellationToken);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await counters.ApplySubscriberDeltaAsync(userId.Value, pageId.Value, delta: -1, cancellationToken);
                return;
            }
            catch (EngagementConcurrencyException)
            {
                if (isLast)
                    break;

                continue;
            }
        }

        throw new UnavailableException("The unfollow could not be saved.");
    }
}
