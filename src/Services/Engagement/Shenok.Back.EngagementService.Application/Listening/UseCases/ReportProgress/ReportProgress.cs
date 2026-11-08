using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Application.Listening.UseCases.ReportProgress;

public sealed record ReportProgressRequest(
    Guid UserId,
    Guid VoiceId,
    string PlaySessionId,
    int PositionSeconds,
    bool Completed,
    bool IsSeek);

public sealed class ReportProgressValidator : AbstractValidator<ReportProgressRequest>
{
    public ReportProgressValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.VoiceId).NotEmpty();
        RuleFor(x => x.PlaySessionId).ClientToken(PlaySessionId.MaxLength, "Play session id");
        RuleFor(x => x.PositionSeconds).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// Stores an accepted progress report and counts the play session once.
/// Does not check unlock. The Gateway calls <c>CanPlay</c> before asking for progress to be stored.
/// </summary>
public sealed class ReportProgressUseCase(
    IListeningRecordRepository listeningRecords,
    IPlaySessionRepository sessions,
    IContentCounters counters,
    IUnitOfWork unitOfWork) : IUseCase<ReportProgressRequest>
{
    public async Task ExecuteAsync(ReportProgressRequest request, CancellationToken cancellationToken)
    {
        var userId = UserId.From(request.UserId);
        var voiceId = VoiceId.From(request.VoiceId);
        var sessionId = PlaySessionId.From(request.PlaySessionId);
        var position = PlaybackPosition.From(request.PositionSeconds);

        var accepted = await SaveListeningAsync(
            userId,
            voiceId,
            sessionId,
            position,
            request.Completed,
            request.IsSeek,
            cancellationToken);
        if (!accepted)
            return;

        if (await CountPlayAsync(sessionId, userId, voiceId, cancellationToken))
            await counters.ApplyPlayAsync(sessionId.Value, voiceId.Value, cancellationToken);
    }

    private async Task<bool> SaveListeningAsync(
        UserId userId,
        VoiceId voiceId,
        PlaySessionId sessionId,
        PlaybackPosition position,
        bool completed,
        bool isSeek,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < EngagementPersistence.MaxAttempts; attempt++)
        {
            var isLast = attempt == EngagementPersistence.MaxAttempts - 1;
            var existing = await listeningRecords.GetAsync(userId, voiceId, cancellationToken);
            var record = existing ?? ListeningRecord.Start(userId, voiceId);
            var report = record.ReportProgress(position, completed, isSeek);
            if (!report.Accepted)
                return false;

            var session = await sessions.GetAsync(sessionId, cancellationToken);
            if (session is not null && !session.BelongsTo(userId, voiceId))
                throw new ConflictException(EngagementConflict.PlaySessionBelongsToAnotherListener);

            if (existing is null)
                await listeningRecords.AddAsync(record, cancellationToken);
            else
                await listeningRecords.UpdateAsync(record, cancellationToken);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (EngagementInsertConflictException exception)
                when (!isLast && existing is null && exception.Target == EngagementInsertTarget.ListeningRecord)
            {
                continue;
            }
            catch (EngagementConcurrencyException) when (!isLast && existing is not null)
            {
                continue;
            }
        }

        throw new InvalidOperationException("Listening progress could not be saved.");
    }

    private async Task<bool> CountPlayAsync(
        PlaySessionId sessionId,
        UserId userId,
        VoiceId voiceId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < EngagementPersistence.MaxAttempts; attempt++)
        {
            var isLast = attempt == EngagementPersistence.MaxAttempts - 1;
            var session = await sessions.GetAsync(sessionId, cancellationToken);
            if (session is null)
            {
                session = PlaySession.Open(sessionId, userId, voiceId);
                var counted = session.AcceptFirstReport();
                await sessions.AddAsync(session, cancellationToken);
                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return counted;
                }
                catch (EngagementInsertConflictException exception)
                    when (!isLast && exception.Target == EngagementInsertTarget.PlaySession)
                {
                    continue;
                }
            }

            if (!session.BelongsTo(userId, voiceId))
                throw new ConflictException(EngagementConflict.PlaySessionBelongsToAnotherListener);

            if (!session.AcceptFirstReport())
                return false;

            await sessions.UpdateAsync(session, cancellationToken);
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (EngagementConcurrencyException) when (!isLast)
            {
                continue;
            }
        }

        throw new InvalidOperationException("The play session could not be saved.");
    }
}
