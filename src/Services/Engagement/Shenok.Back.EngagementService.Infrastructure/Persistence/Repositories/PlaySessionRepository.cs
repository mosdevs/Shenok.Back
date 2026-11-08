using Microsoft.EntityFrameworkCore;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Repositories;

public sealed class PlaySessionRepository(EngagementDbContext db) : IPlaySessionRepository
{
    public Task<PlaySession?> GetAsync(PlaySessionId playSessionId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(playSessionId);
        return db.PlaySessions.AsTracking()
            .FirstOrDefaultAsync(session => session.Id == playSessionId, ct);
    }

    public Task AddAsync(PlaySession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        return db.PlaySessions.AddAsync(session, ct).AsTask();
    }

    public Task UpdateAsync(PlaySession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        db.PlaySessions.Update(session);
        return Task.CompletedTask;
    }
}
