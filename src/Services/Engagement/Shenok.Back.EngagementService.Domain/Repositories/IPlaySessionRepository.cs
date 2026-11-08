using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;

namespace Shenok.Back.EngagementService.Domain.Repositories;

public interface IPlaySessionRepository
{
    Task<PlaySession?> GetAsync(PlaySessionId playSessionId, CancellationToken ct = default);
    Task AddAsync(PlaySession session, CancellationToken ct = default);
    Task UpdateAsync(PlaySession session, CancellationToken ct = default);
}
