using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Application.Follows.UseCases.FollowPage;

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
/// Follows a page once. A second follow does not call the subscriber port.
/// </summary>
public sealed class FollowPageUseCase(
    IUserPageRelationshipRepository relationships,
    IContentCounters counters,
    IUnitOfWork unitOfWork) : IUseCase<FollowPageRequest>
{
    public async Task ExecuteAsync(FollowPageRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId.From(request.UserId);
        var pageId = PageId.From(request.PageId);

        for (var attempt = 0; attempt < EngagementPersistence.MaxAttempts; attempt++)
        {
            var isLast = attempt == EngagementPersistence.MaxAttempts - 1;
            var relationship = await relationships.GetAsync(userId, pageId, cancellationToken);
            var isNew = relationship is null;
            relationship ??= UserPageRelationship.Start(userId, pageId);

            if (!relationship.Follow())
                return;

            if (isNew)
                await relationships.AddAsync(relationship, cancellationToken);
            else
                await relationships.UpdateAsync(relationship, cancellationToken);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await counters.ApplySubscriberDeltaAsync(userId.Value, pageId.Value, delta: 1, cancellationToken);
                return;
            }
            catch (EngagementInsertConflictException exception)
                when (!isLast && isNew && exception.Target == EngagementInsertTarget.UserPageRelationship)
            {
                continue;
            }
            catch (EngagementConcurrencyException) when (!isLast && !isNew)
            {
                continue;
            }
        }

        throw new InvalidOperationException("The follow could not be saved.");
    }
}
