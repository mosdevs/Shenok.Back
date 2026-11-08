using Microsoft.EntityFrameworkCore;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Repositories;

public sealed class UserPageRelationshipRepository(EngagementDbContext db) : IUserPageRelationshipRepository
{
    public Task<UserPageRelationship?> GetAsync(UserId userId, PageId pageId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(pageId);
        return db.UserPageRelationships.AsTracking()
            .FirstOrDefaultAsync(relationship => relationship.UserId == userId && relationship.PageId == pageId, ct);
    }

    public Task AddAsync(UserPageRelationship relationship, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        return db.UserPageRelationships.AddAsync(relationship, ct).AsTask();
    }

    public Task UpdateAsync(UserPageRelationship relationship, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        db.UserPageRelationships.Update(relationship);
        return Task.CompletedTask;
    }
}
