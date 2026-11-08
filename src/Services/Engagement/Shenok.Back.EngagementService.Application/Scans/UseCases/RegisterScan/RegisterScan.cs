using FluentValidation;
using Microsoft.Extensions.Options;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Application.Scans.UseCases.RegisterScan;

public sealed record RegisterScanRequest(
    string ScanId,
    Guid UserId,
    Guid WaveformId,
    Guid VoiceId,
    Guid PageId);

public sealed record RegisterScanResponse(
    bool Unlocked,
    bool IsFollowing,
    bool PromptFollow,
    int ResumePositionSeconds);

public sealed class RegisterScanValidator : AbstractValidator<RegisterScanRequest>
{
    public RegisterScanValidator()
    {
        RuleFor(x => x.ScanId).ClientToken(ScanId.MaxLength, "Scan id");
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.WaveformId).NotEmpty();
        RuleFor(x => x.VoiceId).NotEmpty();
        RuleFor(x => x.PageId).NotEmpty();
    }
}

/// <summary>
/// Unlocks a page for one scan id and freezes the response on the receipt.
/// A later follow or seek does not change what the same scan id returns.
/// </summary>
public sealed class RegisterScanUseCase(
    IScanReceiptRepository receipts,
    IUserPageRelationshipRepository relationships,
    IListeningRecordRepository listeningRecords,
    IContentCounters counters,
    IUnitOfWork unitOfWork,
    IOptions<EngagementOptions> options) : IUseCase<RegisterScanRequest, RegisterScanResponse>
{
    public async Task<RegisterScanResponse> ExecuteAsync(
        RegisterScanRequest request,
        CancellationToken cancellationToken)
    {
        var scanId = ScanId.From(request.ScanId);
        var userId = UserId.From(request.UserId);
        var waveformId = WaveformId.From(request.WaveformId);
        var voiceId = VoiceId.From(request.VoiceId);
        var pageId = PageId.From(request.PageId);

        var stored = await receipts.GetAsync(scanId, cancellationToken);
        if (stored is not null)
            return OwnedSnapshot(stored, userId);

        var listening = await listeningRecords.GetAsync(userId, voiceId, cancellationToken);
        var resume = listening?.Position ?? PlaybackPosition.Zero;

        for (var attempt = 0; attempt < EngagementPersistence.MaxAttempts; attempt++)
        {
            var isLast = attempt == EngagementPersistence.MaxAttempts - 1;
            var relationship = await relationships.GetAsync(userId, pageId, cancellationToken);
            var isNew = relationship is null;
            relationship ??= UserPageRelationship.Start(userId, pageId);

            var unlockedNow = relationship.Unlock();
            var followed = options.Value.AutoFollowOnScan && relationship.Follow();

            var receipt = ScanReceipt.Record(
                scanId,
                userId,
                waveformId,
                voiceId,
                pageId,
                unlocked: relationship.UnlockedAt.HasValue,
                isFollowing: relationship.IsFollowing,
                promptFollow: !relationship.IsFollowing,
                resume);

            if (isNew)
                await relationships.AddAsync(relationship, cancellationToken);
            else if (unlockedNow || followed)
                await relationships.UpdateAsync(relationship, cancellationToken);

            await receipts.AddAsync(receipt, cancellationToken);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                if (followed)
                {
                    await counters.ApplySubscriberDeltaAsync(
                        userId.Value,
                        pageId.Value,
                        delta: 1,
                        cancellationToken);
                }

                return ToResponse(receipt);
            }
            catch (EngagementInsertConflictException exception) when (exception.Target == EngagementInsertTarget.ScanReceipt)
            {
                var winner = await receipts.GetAsync(scanId, cancellationToken)
                    ?? throw new InvalidOperationException("The scan id that won the insert was not found.");
                return OwnedSnapshot(winner, userId);
            }
            catch (EngagementInsertConflictException exception)
                when (isNew && exception.Target == EngagementInsertTarget.UserPageRelationship)
            {
                if (isLast)
                    break;

                continue;
            }
            catch (EngagementConcurrencyException) when (!isNew)
            {
                if (isLast)
                    break;

                continue;
            }
        }

        throw new UnavailableException("The scan could not be stored.");
    }

    private static RegisterScanResponse OwnedSnapshot(ScanReceipt receipt, UserId userId)
    {
        if (!receipt.BelongsTo(userId))
            throw new ConflictException(EngagementConflict.ScanBelongsToAnotherUser);

        return ToResponse(receipt);
    }

    private static RegisterScanResponse ToResponse(ScanReceipt receipt) =>
        new(receipt.Unlocked, receipt.IsFollowing, receipt.PromptFollow, receipt.ResumePosition.Value);
}
