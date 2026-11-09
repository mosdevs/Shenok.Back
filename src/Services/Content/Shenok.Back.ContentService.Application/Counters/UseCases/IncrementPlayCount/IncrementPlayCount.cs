using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Repositories;

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
/// Phase 6 applies the play change. A missing voice is rejected first. The count is not changed.
/// </summary>
public sealed class IncrementPlayCountUseCase(IContentCaller caller, IVoiceRepository voices)
    : IUseCase<IncrementPlayCountRequest, IncrementPlayCountResponse>
{
    public async Task<IncrementPlayCountResponse> ExecuteAsync(
        IncrementPlayCountRequest request,
        CancellationToken cancellationToken)
    {
        PageAccess.EnsureInternal(caller);
        if (request.VoiceId == Guid.Empty)
            throw new NotFoundException("Voice was not found.");
        if (request.PlaySessionId == Guid.Empty)
            throw new NotFoundException("Play session was not found.");

        IdempotencyKey.Require(request.PlaySessionId.ToString("D"));
        _ = await voices.GetByIdAsync(VoiceId.From(request.VoiceId), cancellationToken)
            ?? throw new NotFoundException("Voice was not found.");
        throw new CounterUpdateDeferredException();
    }
}
