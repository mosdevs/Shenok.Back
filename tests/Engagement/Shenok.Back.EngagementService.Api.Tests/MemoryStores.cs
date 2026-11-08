using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Api.Tests;

/// <summary>
/// Stages inserts until save. A primary-key clash discards the whole batch and throws
/// <see cref="EngagementInsertConflictException"/> so the use case can reload the winner.
/// </summary>
internal sealed class EngagementMemory : IUnitOfWork
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, ScanReceipt> _receipts = new(StringComparer.Ordinal);
    private readonly Dictionary<UserPageRelationshipId, UserPageRelationship> _relationships = [];
    private readonly Dictionary<ListeningRecordId, ListeningRecord> _listening = [];
    private readonly Dictionary<string, PlaySession> _sessions = new(StringComparer.Ordinal);
    private readonly List<PendingChange> _pending = [];

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            foreach (var change in _pending)
            {
                if (!change.Conflicts())
                    continue;

                var target = change.Target;
                _pending.Clear();
                throw new EngagementInsertConflictException(target);
            }

            foreach (var change in _pending)
                change.Commit();

            _pending.Clear();
            return Task.FromResult(1);
        }
    }

    public void Insert(ScanReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        Stage(
            () => _receipts.ContainsKey(receipt.Id.Value),
            EngagementInsertTarget.ScanReceipt,
            () => _receipts[receipt.Id.Value] = receipt);
    }

    public void Insert(UserPageRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        Stage(
            () => _relationships.ContainsKey(relationship.Id),
            EngagementInsertTarget.UserPageRelationship,
            () => _relationships[relationship.Id] = relationship);
    }

    public void Insert(ListeningRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Stage(
            () => _listening.ContainsKey(record.Id),
            EngagementInsertTarget.ListeningRecord,
            () => _listening[record.Id] = record);
    }

    public void Insert(PlaySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Stage(
            () => _sessions.ContainsKey(session.Id.Value),
            EngagementInsertTarget.PlaySession,
            () => _sessions[session.Id.Value] = session);
    }

    public void Update(UserPageRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        Stage(conflicts: null, target: null, () => _relationships[relationship.Id] = relationship);
    }

    public void Update(ListeningRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Stage(conflicts: null, target: null, () => _listening[record.Id] = record);
    }

    public void Update(PlaySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Stage(conflicts: null, target: null, () => _sessions[session.Id.Value] = session);
    }

    public ScanReceipt? Find(ScanId scanId)
    {
        lock (_gate)
            return _receipts.GetValueOrDefault(scanId.Value);
    }

    public UserPageRelationship? Find(UserId userId, PageId pageId)
    {
        lock (_gate)
            return _relationships.GetValueOrDefault(new UserPageRelationshipId(userId, pageId));
    }

    public ListeningRecord? Find(UserId userId, VoiceId voiceId)
    {
        lock (_gate)
            return _listening.GetValueOrDefault(new ListeningRecordId(userId, voiceId));
    }

    public PlaySession? Find(PlaySessionId sessionId)
    {
        lock (_gate)
            return _sessions.GetValueOrDefault(sessionId.Value);
    }

    public IReadOnlyList<ListeningRecord> List(UserId userId, IReadOnlyCollection<VoiceId> voiceIds)
    {
        var wanted = voiceIds.Select(id => id.Value).ToHashSet();
        lock (_gate)
        {
            return _listening.Values
                .Where(record => record.UserId == userId && wanted.Contains(record.VoiceId.Value))
                .ToArray();
        }
    }

    private void Stage(Func<bool>? conflicts, EngagementInsertTarget? target, Action commit)
    {
        lock (_gate)
            _pending.Add(new PendingChange(conflicts, target, commit));
    }

    private sealed class PendingChange(Func<bool>? conflicts, EngagementInsertTarget? target, Action commit)
    {
        public bool Conflicts() => conflicts?.Invoke() == true;

        public EngagementInsertTarget Target =>
            target ?? throw new InvalidOperationException("An update cannot lose an insert race.");

        public void Commit() => commit();
    }
}

internal sealed class MemoryReceipts(EngagementMemory memory) : IScanReceiptRepository
{
    public Task AddAsync(ScanReceipt receipt, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        memory.Insert(receipt);
        return Task.CompletedTask;
    }

    public Task<ScanReceipt?> GetAsync(ScanId scanId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(memory.Find(scanId));
    }
}

internal sealed class MemoryRelationships(EngagementMemory memory) : IUserPageRelationshipRepository
{
    public Task AddAsync(UserPageRelationship relationship, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        memory.Insert(relationship);
        return Task.CompletedTask;
    }

    public Task<UserPageRelationship?> GetAsync(UserId userId, PageId pageId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(memory.Find(userId, pageId));
    }

    public Task UpdateAsync(UserPageRelationship relationship, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        memory.Update(relationship);
        return Task.CompletedTask;
    }
}

internal sealed class MemoryListening(EngagementMemory memory) : IListeningRecordRepository
{
    public Task AddAsync(ListeningRecord listeningRecord, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        memory.Insert(listeningRecord);
        return Task.CompletedTask;
    }

    public Task<ListeningRecord?> GetAsync(UserId userId, VoiceId voiceId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(memory.Find(userId, voiceId));
    }

    public Task<IReadOnlyList<ListeningRecord>> ListAsync(
        UserId userId,
        IReadOnlyCollection<VoiceId> voiceIds,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(memory.List(userId, voiceIds));
    }

    public Task UpdateAsync(ListeningRecord listeningRecord, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        memory.Update(listeningRecord);
        return Task.CompletedTask;
    }
}

internal sealed class MemorySessions(EngagementMemory memory) : IPlaySessionRepository
{
    public Task AddAsync(PlaySession session, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        memory.Insert(session);
        return Task.CompletedTask;
    }

    public Task<PlaySession?> GetAsync(PlaySessionId playSessionId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(memory.Find(playSessionId));
    }

    public Task UpdateAsync(PlaySession session, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        memory.Update(session);
        return Task.CompletedTask;
    }
}
