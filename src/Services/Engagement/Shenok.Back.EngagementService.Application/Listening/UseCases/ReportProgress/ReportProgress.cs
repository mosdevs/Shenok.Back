using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.DomainEvents;
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
/// The listening row and the play session commit in one save.
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

        var playCommandId = await SaveReportAsync(
            userId,
            voiceId,
            sessionId,
            position,
            request.Completed,
            request.IsSeek,
            cancellationToken);
        if (playCommandId is Guid commandId)
            await counters.ApplyPlayAsync(sessionId.Value, voiceId.Value, commandId, cancellationToken);
        else
            await counters.DeliverPendingPlayAsync(sessionId.Value, cancellationToken);
    }

    private async Task<Guid?> SaveReportAsync(
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
            if (!record.ReportProgress(position, completed, isSeek).Accepted)
                return null;

            var session = await sessions.GetAsync(sessionId, cancellationToken);
            if (session is not null && !session.BelongsTo(userId, voiceId))
                throw new ConflictException(EngagementConflict.PlaySessionBelongsToAnotherListener);

            var isNewRecord = existing is null;
            if (isNewRecord)
                await listeningRecords.AddAsync(record, cancellationToken);
            else
                await listeningRecords.UpdateAsync(record, cancellationToken);

            Guid? playCommandId = null;
            if (session is null)
            {
                session = PlaySession.Open(sessionId, userId, voiceId);
                playCommandId = AcceptPlay(session);
                await sessions.AddAsync(session, cancellationToken);
            }
            else if (session.AcceptFirstReport())
            {
                playCommandId = PlayCommandId(session);
                await sessions.UpdateAsync(session, cancellationToken);
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return playCommandId;
            }
            catch (EngagementInsertConflictException exception) when (exception.Target == EngagementInsertTarget.PlaySession)
            {
                var winner = await sessions.GetAsync(sessionId, cancellationToken)
                    ?? throw new InvalidOperationException("The play session that won the insert was not found.");
                if (!winner.BelongsTo(userId, voiceId))
                {
                    await SaveListeningAsync(userId, voiceId, position, completed, isSeek, cancellationToken);
                    throw new ConflictException(EngagementConflict.PlaySessionBelongsToAnotherListener);
                }

                if (!isLast)
                    continue;

                return await SaveListeningAsync(userId, voiceId, position, completed, isSeek, cancellationToken, winner.Id);
            }
            catch (EngagementInsertConflictException exception)
                when (isNewRecord && exception.Target == EngagementInsertTarget.ListeningRecord)
            {
                if (isLast)
                    break;

                continue;
            }
            catch (EngagementConcurrencyException)
            {
                if (isLast)
                    break;

                continue;
            }
        }

        throw new UnavailableException("Listening progress could not be saved.");
    }

    /// <summary>
    /// Writes the listening report after a combined save rolled back.
    /// When <paramref name="ownedSessionId"/> is set, a still-uncounted session is updated in the same save.
    /// The returned id is the play command stored in that save.
    /// </summary>
    private async Task<Guid?> SaveListeningAsync(
        UserId userId,
        VoiceId voiceId,
        PlaybackPosition position,
        bool completed,
        bool isSeek,
        CancellationToken cancellationToken,
        PlaySessionId? ownedSessionId = null)
    {
        for (var attempt = 0; attempt < EngagementPersistence.MaxAttempts; attempt++)
        {
            var isLast = attempt == EngagementPersistence.MaxAttempts - 1;
            var existing = await listeningRecords.GetAsync(userId, voiceId, cancellationToken);
            var record = existing ?? ListeningRecord.Start(userId, voiceId);
            if (!record.ReportProgress(position, completed, isSeek).Accepted)
                return null;

            var isNewRecord = existing is null;
            if (isNewRecord)
                await listeningRecords.AddAsync(record, cancellationToken);
            else
                await listeningRecords.UpdateAsync(record, cancellationToken);

            Guid? playCommandId = null;
            if (ownedSessionId is not null)
            {
                var session = await sessions.GetAsync(ownedSessionId, cancellationToken)
                    ?? throw new InvalidOperationException("The play session that won the insert was not found.");
                if (!session.BelongsTo(userId, voiceId))
                    throw new ConflictException(EngagementConflict.PlaySessionBelongsToAnotherListener);

                if (session.AcceptFirstReport())
                {
                    playCommandId = PlayCommandId(session);
                    await sessions.UpdateAsync(session, cancellationToken);
                }
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return playCommandId;
            }
            catch (EngagementInsertConflictException exception)
                when (isNewRecord && exception.Target == EngagementInsertTarget.ListeningRecord)
            {
                if (isLast)
                    break;

                continue;
            }
            catch (EngagementConcurrencyException)
            {
                if (isLast)
                    break;

                continue;
            }
        }

        throw new UnavailableException("Listening progress could not be saved.");
    }

    private static Guid AcceptPlay(PlaySession session)
    {
        if (!session.AcceptFirstReport())
            throw new InvalidOperationException("A new play session was already counted.");

        return PlayCommandId(session);
    }

    private static Guid PlayCommandId(PlaySession session) =>
        session.DomainEvents.OfType<VoicePlayedDomainEvent>().Last().EventId;
}
