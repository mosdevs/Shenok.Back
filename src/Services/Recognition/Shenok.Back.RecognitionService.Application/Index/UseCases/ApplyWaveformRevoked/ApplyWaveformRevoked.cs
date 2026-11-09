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
    {
        RuleFor(request => request.EventId).NotEmpty();
        RuleFor(request => request.WaveformId).NotEmpty();
        RuleFor(request => request.TargetId).NotEmpty();
        RuleFor(request => request.WaveformCode).InclusiveBetween(1L, WaveformCode.MaxValue);
        RuleFor(request => request.LayoutVersion).GreaterThanOrEqualTo(LayoutVersion.Version1);
        RuleFor(request => request.TargetType)
            .Must(type => type is nameof(WaveformTargetType.Page) or nameof(WaveformTargetType.Voice))
            .WithMessage("Target type must be Page or Voice.");
        RuleFor(request => request.OccurredOn).Must(occurredOn => occurredOn != default);
    }
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
