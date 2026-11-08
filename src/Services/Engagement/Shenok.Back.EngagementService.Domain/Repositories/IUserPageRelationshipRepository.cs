using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

namespace Shenok.Back.EngagementService.Domain.Repositories;

public interface IUserPageRelationshipRepository
{
    Task<UserPageRelationship?> GetAsync(UserId userId, PageId pageId, CancellationToken ct = default);
    Task AddAsync(UserPageRelationship relationship, CancellationToken ct = default);
    Task UpdateAsync(UserPageRelationship relationship, CancellationToken ct = default);
}
