using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

namespace Shenok.Back.EngagementService.Domain.Repositories;

public interface IListeningRecordRepository
{
    Task<ListeningRecord?> GetAsync(UserId userId, VoiceId voiceId, CancellationToken ct = default);

    /// <summary>
    /// Loads the rows this user already has for the given voices.
    /// Voice ids with no row are omitted. An empty <paramref name="voiceIds"/> yields an empty list.
    /// </summary>
    Task<IReadOnlyList<ListeningRecord>> ListAsync(
        UserId userId,
        IReadOnlyCollection<VoiceId> voiceIds,
        CancellationToken ct = default);

    Task AddAsync(ListeningRecord listeningRecord, CancellationToken ct = default);
    Task UpdateAsync(ListeningRecord listeningRecord, CancellationToken ct = default);
}
