using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;

namespace Shenok.Back.ContentService.Application.Counters.UseCases.IncrementPlayCount;

public sealed record IncrementPlayCountRequest(Guid VoiceId, Guid PlaySessionId);

public sealed record IncrementPlayCountResponse(Guid VoiceId, long PlayCount, bool Applied);

public sealed class IncrementPlayCountValidator : AbstractValidator<IncrementPlayCountRequest>
{
    public IncrementPlayCountValidator()
    {
        RuleFor(x => x.VoiceId).NotEmpty();
        RuleFor(x => x.PlaySessionId).NotEmpty();
    }
}

/// <summary>
/// Phase 2 reserves the play command. The counter row is not loaded and is not changed.
/// </summary>
public sealed class IncrementPlayCountUseCase(IContentCaller caller)
    : IUseCase<IncrementPlayCountRequest, IncrementPlayCountResponse>
{
    public Task<IncrementPlayCountResponse> ExecuteAsync(
        IncrementPlayCountRequest request,
        CancellationToken cancellationToken)
    {
        PageAccess.EnsureInternal(caller);
        if (request.VoiceId == Guid.Empty)
            throw new NotFoundException("Voice was not found.");
        if (request.PlaySessionId == Guid.Empty)
            throw new NotFoundException("Play session was not found.");

        IdempotencyKey.Require(request.PlaySessionId.ToString("D"));
        throw new CounterUpdateDeferredException();
    }
}
