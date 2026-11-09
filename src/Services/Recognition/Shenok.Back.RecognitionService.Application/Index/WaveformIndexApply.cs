using Microsoft.Extensions.Logging;
using Shenok.Back.RecognitionService.Application.Common;
using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Application.Index;

/// <summary>
/// One bus fact applied to the index, then recorded in the inbox.
/// Register and revoke share this sequence. They do not call each other.
/// </summary>
internal static class WaveformIndexApply
{
    public const int MaxAttempts = 3;

    public static async Task ExecuteAsync(
        WaveformApplyRequest request,
        WaveformMessageType messageType,
        Func<WaveformFact, WaveformIndexEntry> createFirst,
        Func<WaveformIndexEntry, WaveformFact, IndexApplyResult> apply,
        IWaveformIndexRepository index,
        IInbox inbox,
        IUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(createFirst);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(inbox);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        if (await inbox.ExistsAsync(request.EventId, cancellationToken))
            return;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await ApplyOnceAsync(
                    request,
                    messageType,
                    createFirst,
                    apply,
                    index,
                    inbox,
                    unitOfWork,
                    logger,
                    cancellationToken);
                return;
            }
            catch (InboxAlreadyProcessedException)
            {
                return;
            }
            catch (IndexConcurrencyException) when (attempt < MaxAttempts)
            {
                logger.LogDebug(
                    "Waveform index write conflicted on attempt {Attempt} of {MaxAttempts} for event {EventId}. Reloading.",
                    attempt,
                    MaxAttempts,
                    request.EventId);
            }
        }

        throw new InvalidOperationException("Waveform index apply ended without a result.");
    }

    private static async Task ApplyOnceAsync(
        WaveformApplyRequest request,
        WaveformMessageType messageType,
        Func<WaveformFact, WaveformIndexEntry> createFirst,
        Func<WaveformIndexEntry, WaveformFact, IndexApplyResult> apply,
        IWaveformIndexRepository index,
        IInbox inbox,
        IUnitOfWork unitOfWork,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var fact = CreateFact(request);
        var existing = await index.GetAsync(fact.Code, cancellationToken);
        if (existing is null)
        {
            await index.AddAsync(createFirst(fact), cancellationToken);
        }
        else
        {
            var result = apply(existing, fact);
            if (result == IndexApplyResult.IgnoredConflict)
            {
                logger.LogWarning(
                    "Waveform index ignored a conflicting {MessageType} for code {WaveformCode}. Stored waveform {StoredWaveformId} target {StoredTargetType} {StoredTargetId}. Incoming waveform {IncomingWaveformId} target {IncomingTargetType} {IncomingTargetId}.",
                    messageType,
                    fact.Code.Value,
                    existing.WaveformId.Value,
                    existing.TargetType,
                    existing.TargetId,
                    fact.WaveformId.Value,
                    fact.TargetType,
                    fact.TargetId);
            }
        }

        await inbox.AddAsync(request.EventId, messageType, DateTime.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static WaveformFact CreateFact(WaveformApplyRequest request)
    {
        var occurredOn = NormalizeOccurredOn(request.OccurredOn);
        return WaveformFact.Create(
            WaveformCode.From(request.WaveformCode),
            WaveformId.From(request.WaveformId),
            ParseTarget(request.TargetType),
            request.TargetId,
            request.LayoutVersion,
            occurredOn,
            request.EventId);
    }

    /// <summary>
    /// Same normalization as Content's outbox: UTC stays, local is converted, and unspecified
    /// is stamped UTC without shifting the ticks. <see cref="DateTime.ToUniversalTime"/> would
    /// treat unspecified as local and move the instant the publisher stored.
    /// </summary>
    private static DateTime NormalizeOccurredOn(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static WaveformTargetType ParseTarget(string targetType)
    {
        if (Enum.TryParse<WaveformTargetType>(targetType, ignoreCase: false, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ArgumentException("Target type must be Page or Voice.", nameof(targetType));
    }
}

/// <summary>
/// The primitive fields both apply requests carry. The use case builds one of these
/// and the domain builds one <see cref="WaveformFact"/> from it.
/// </summary>
internal sealed record WaveformApplyRequest(
    Guid EventId,
    DateTime OccurredOn,
    Guid WaveformId,
    long WaveformCode,
    int LayoutVersion,
    string TargetType,
    Guid TargetId);
