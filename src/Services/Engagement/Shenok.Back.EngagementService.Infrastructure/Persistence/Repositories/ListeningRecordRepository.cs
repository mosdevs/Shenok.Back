using Microsoft.EntityFrameworkCore;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Repositories;

public sealed class ListeningRecordRepository(EngagementDbContext db) : IListeningRecordRepository
{
    public Task<ListeningRecord?> GetAsync(UserId userId, VoiceId voiceId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(voiceId);
        return db.ListeningRecords.AsTracking()
            .FirstOrDefaultAsync(record => record.UserId == userId && record.VoiceId == voiceId, ct);
    }

    public async Task<IReadOnlyList<ListeningRecord>> ListAsync(
        UserId userId,
        IReadOnlyCollection<VoiceId> voiceIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(voiceIds);
        if (voiceIds.Count == 0)
            return [];

        var ids = voiceIds.ToArray();
        return await db.ListeningRecords.AsNoTracking()
            .Where(record => record.UserId == userId && ids.Contains(record.VoiceId))
            .ToListAsync(ct);
    }

    public Task AddAsync(ListeningRecord listeningRecord, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(listeningRecord);
        return db.ListeningRecords.AddAsync(listeningRecord, ct).AsTask();
    }

    public Task UpdateAsync(ListeningRecord listeningRecord, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(listeningRecord);
        db.ListeningRecords.Update(listeningRecord);
        return Task.CompletedTask;
    }
}
