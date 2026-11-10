using FluentValidation;
using Microsoft.Extensions.Logging;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Application.Index;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRevoked;

public sealed record ApplyWaveformRevokedRequest(
    Guid EventId,
    DateTime OccurredOn,
    Guid WaveformId,
    long WaveformCode,
    int LayoutVersion,
    string TargetType,
    Guid TargetId);

public sealed class ApplyWaveformRevokedValidator : AbstractValidator<ApplyWaveformRevokedRequest>
{
    public ApplyWaveformRevokedValidator()
        => WaveformApplyRules.Add(this);
}

/// <summary>
/// Projects <c>WaveformRevoked</c> onto the index and records the event id.
/// A revoke that arrives before its register inserts a tombstone. A replay returns without writing.
/// </summary>
public sealed class ApplyWaveformRevokedUseCase(
    IWaveformIndexRepository index,
    IInbox inbox,
    IUnitOfWork unitOfWork,
    ILogger<ApplyWaveformRevokedUseCase> logger) : IUseCase<ApplyWaveformRevokedRequest>
{
    public Task ExecuteAsync(ApplyWaveformRevokedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WaveformIndexApply.ExecuteAsync(
            ToApplyRequest(request),
            WaveformMessageType.WaveformRevoked,
            WaveformIndexEntry.RevokeFirst,
            static (entry, fact) => entry.ApplyRevoked(fact),
            index,
            inbox,
            unitOfWork,
            logger,
            cancellationToken);
    }

    private static WaveformApplyRequest ToApplyRequest(ApplyWaveformRevokedRequest request)
        => new(
            request.EventId,
            request.OccurredOn,
            request.WaveformId,
            request.WaveformCode,
            request.LayoutVersion,
            request.TargetType,
            request.TargetId);
}
