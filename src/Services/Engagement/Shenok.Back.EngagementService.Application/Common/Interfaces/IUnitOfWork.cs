namespace Shenok.Back.EngagementService.Application.Common.Interfaces;

/// <summary>
/// Commits the current Engagement unit of work.
/// The database context's synchronous <c>SaveChanges</c> throws; callers use this method.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
