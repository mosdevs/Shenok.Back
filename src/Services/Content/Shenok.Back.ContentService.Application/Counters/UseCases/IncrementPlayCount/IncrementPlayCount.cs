using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Counters.UseCases.IncrementPlayCount;

public sealed record IncrementPlayCountRequest(Guid VoiceId, string PlaySessionId);

public sealed record IncrementPlayCountResponse(Guid VoiceId, long PlayCount, bool Applied);

public sealed class IncrementPlayCountValidator : AbstractValidator<IncrementPlayCountRequest>
{
    public IncrementPlayCountValidator()
    {
        RuleFor(x => x.VoiceId).NotEmpty();
        RuleFor(x => x.PlaySessionId).NotEmpty().MaximumLength(IdempotencyKey.PlaySessionMaxLength);
    }
}

/// <summary>
/// Counts one play session. A second delivery of the same session id does not add another play.
/// </summary>
public sealed class IncrementPlayCountUseCase(
    IContentCaller caller,
    IVoiceRepository voices,
    ICounterLedger ledger) : IUseCase<IncrementPlayCountRequest, IncrementPlayCountResponse>
{
    public async Task<IncrementPlayCountResponse> ExecuteAsync(
        IncrementPlayCountRequest request,
        CancellationToken cancellationToken)
    {
        PageAccess.EnsureInternal(caller);
        if (request.VoiceId == Guid.Empty)
            throw new NotFoundException("Voice was not found.");

        IdempotencyKey.RequirePlaySession(request.PlaySessionId);
        _ = await voices.GetByIdAsync(VoiceId.From(request.VoiceId), cancellationToken)
            ?? throw new NotFoundException("Voice was not found.");

        var update = await ledger.IncrementPlayAsync(request.VoiceId, request.PlaySessionId, cancellationToken);
        if (!update.Found)
            throw new NotFoundException("Voice was not found.");

        return new IncrementPlayCountResponse(request.VoiceId, update.PlayCount, update.Applied);
    }
}
