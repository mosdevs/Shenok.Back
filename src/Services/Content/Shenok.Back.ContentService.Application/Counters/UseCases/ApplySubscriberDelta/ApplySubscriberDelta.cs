using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;

namespace Shenok.Back.ContentService.Application.Counters.UseCases.ApplySubscriberDelta;

public sealed record ApplySubscriberDeltaRequest(Guid PageId, long Delta, string IdempotencyKey);

public sealed record ApplySubscriberDeltaResponse(Guid PageId, long SubscriberCount, bool Applied);

public sealed class ApplySubscriberDeltaValidator : AbstractValidator<ApplySubscriberDeltaRequest>
{
    public ApplySubscriberDeltaValidator()
    {
        RuleFor(x => x.PageId).NotEmpty();
        RuleFor(x => x.Delta).Must(delta => delta is 1 or -1).WithMessage("Subscriber delta must be 1 or -1.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(IdempotencyKey.MaxLength);
    }
}

/// <summary>
/// Phase 2 reserves the follow and unfollow command. The counter row is not loaded and is not changed.
/// </summary>
public sealed class ApplySubscriberDeltaUseCase(IContentCaller caller)
    : IUseCase<ApplySubscriberDeltaRequest, ApplySubscriberDeltaResponse>
{
    public Task<ApplySubscriberDeltaResponse> ExecuteAsync(
        ApplySubscriberDeltaRequest request,
        CancellationToken cancellationToken)
    {
        PageAccess.EnsureInternal(caller);
        if (request.PageId == Guid.Empty)
            throw new NotFoundException("Page was not found.");
        if (request.Delta is not (1 or -1))
            throw new ConflictException("Subscriber delta must be 1 or -1.");

        IdempotencyKey.Require(request.IdempotencyKey);
        throw new CounterUpdateDeferredException();
    }
}
