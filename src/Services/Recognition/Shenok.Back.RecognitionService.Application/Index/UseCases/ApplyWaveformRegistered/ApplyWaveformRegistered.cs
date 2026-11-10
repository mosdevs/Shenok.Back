using FluentValidation;
using Microsoft.Extensions.Logging;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Application.Index;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Application.Index.UseCases.ApplyWaveformRegistered;

public sealed record ApplyWaveformRegisteredRequest(
    Guid EventId,
    DateTime OccurredOn,
    Guid WaveformId,
    long WaveformCode,
    int LayoutVersion,
    string TargetType,
    Guid TargetId);

public sealed class ApplyWaveformRegisteredValidator : AbstractValidator<ApplyWaveformRegisteredRequest>
{
    public ApplyWaveformRegisteredValidator()
        => WaveformApplyRules.Add(this);
}

/// <summary>
/// Projects <c>WaveformRegistered</c> onto the index and records the event id.
/// A second delivery of the same event id returns without writing.
/// </summary>
public sealed class ApplyWaveformRegisteredUseCase(
    IWaveformIndexRepository index,
    IInbox inbox,
    IUnitOfWork unitOfWork,
    ILogger<ApplyWaveformRegisteredUseCase> logger) : IUseCase<ApplyWaveformRegisteredRequest>
{
    public Task ExecuteAsync(ApplyWaveformRegisteredRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WaveformIndexApply.ExecuteAsync(
            ToApplyRequest(request),
            WaveformMessageType.WaveformRegistered,
            WaveformIndexEntry.Register,
            static (entry, fact) => entry.ApplyRegistered(fact),
            index,
            inbox,
            unitOfWork,
            logger,
            cancellationToken);
    }

    private static WaveformApplyRequest ToApplyRequest(ApplyWaveformRegisteredRequest request)
        => new(
            request.EventId,
            request.OccurredOn,
            request.WaveformId,
            request.WaveformCode,
            request.LayoutVersion,
            request.TargetType,
            request.TargetId);
}
