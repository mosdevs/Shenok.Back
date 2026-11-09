using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Repositories;

namespace Shenok.Back.EngagementService.Application.Tests;

/// <summary>
/// Commits on save. A forced failure discards staged inserts, then runs <see cref="BeforeConflict"/>
/// so the reloaded row can be a different instance than the one the use case just mutated.
/// An update of a row that is already stored is visible before save, because the use case mutates that instance.
/// </summary>
internal sealed class EngagementMemory
{
    private readonly Dictionary<string, ScanReceipt> _receipts = new(StringComparer.Ordinal);
    private readonly Dictionary<UserPageRelationshipId, UserPageRelationship> _relationships = [];
    private readonly Dictionary<ListeningRecordId, ListeningRecord> _listening = [];
    private readonly Dictionary<string, PlaySession> _sessions = new(StringComparer.Ordinal);
    private readonly List<Action> _pending = [];
    private int _saveAttempts;

    public int Saves { get; private set; }

    public Exception? Failure { get; set; }

    /// <summary>
    /// 1-based save call that throws <see cref="Failure"/>. Null fails the next save.
    /// </summary>
    public int? FailOnAttempt { get; set; }

    public Action? BeforeConflict { get; set; }

    public IReadOnlyCollection<ScanReceipt> Receipts => _receipts.Values;

    public IReadOnlyCollection<UserPageRelationship> Relationships => _relationships.Values;

    public IReadOnlyCollection<ListeningRecord> ListeningRecords => _listening.Values;

    public IReadOnlyCollection<PlaySession> Sessions => _sessions.Values;

    public ScanReceipt? FindReceipt(ScanId id) => _receipts.GetValueOrDefault(id.Value);

    public UserPageRelationship? FindRelationship(UserId userId, PageId pageId) =>
        _relationships.GetValueOrDefault(new UserPageRelationshipId(userId, pageId));

    public ListeningRecord? FindListening(UserId userId, VoiceId voiceId) =>
        _listening.GetValueOrDefault(new ListeningRecordId(userId, voiceId));

    public PlaySession? FindSession(PlaySessionId id) => _sessions.GetValueOrDefault(id.Value);

    public void Seed(ScanReceipt receipt) => _receipts[receipt.Id.Value] = receipt;

    public void Seed(UserPageRelationship relationship) => _relationships[relationship.Id] = relationship;

    public void Seed(ListeningRecord record) => _listening[record.Id] = record;

    public void Seed(PlaySession session) => _sessions[session.Id.Value] = session;

    public void Stage(Action commit) => _pending.Add(commit);

    public int Save()
    {
        _saveAttempts++;
        if (Failure is not null && (FailOnAttempt is null || FailOnAttempt == _saveAttempts))
        {
            var failure = Failure;
            Failure = null;
            FailOnAttempt = null;
            _pending.Clear();
            var before = BeforeConflict;
            BeforeConflict = null;
            before?.Invoke();
            throw failure;
        }

        foreach (var commit in _pending)
            commit();

        _pending.Clear();
        Saves++;
        return 1;
    }
}

internal sealed class FakeUnitOfWork(EngagementMemory memory) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(memory.Save());
    }
}

internal sealed class FakeContentCounters : IContentCounters
{
    public List<(Guid UserId, Guid PageId, int Delta)> SubscriberDeltas { get; } = [];

    public List<(string PlaySessionId, Guid VoiceId)> Plays { get; } = [];

    public Task ApplySubscriberDeltaAsync(Guid userId, Guid pageId, int delta, Guid commandId, CancellationToken ct = default)
    {
        SubscriberDeltas.Add((userId, pageId, delta));
        return Task.CompletedTask;
    }

    public Task DeliverPendingSubscriberAsync(Guid userId, Guid pageId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task ApplyPlayAsync(string playSessionId, Guid voiceId, Guid commandId, CancellationToken ct = default)
    {
        Plays.Add((playSessionId, voiceId));
        return Task.CompletedTask;
    }

    public Task DeliverPendingPlayAsync(string playSessionId, CancellationToken ct = default) =>
        Task.CompletedTask;
}

internal sealed class FakeReceipts(EngagementMemory memory) : IScanReceiptRepository
{
    public int Gets { get; private set; }

    public Task AddAsync(ScanReceipt receipt, CancellationToken ct = default)
    {
        memory.Stage(() => memory.Seed(receipt));
        return Task.CompletedTask;
    }

    public Task<ScanReceipt?> GetAsync(ScanId scanId, CancellationToken ct = default)
    {
        Gets++;
        return Task.FromResult(memory.FindReceipt(scanId));
    }
}

internal sealed class FakeRelationships(EngagementMemory memory) : IUserPageRelationshipRepository
{
    public int Gets { get; private set; }

    public int Adds { get; private set; }

    public Task AddAsync(UserPageRelationship relationship, CancellationToken ct = default)
    {
        Adds++;
        memory.Stage(() => memory.Seed(relationship));
        return Task.CompletedTask;
    }

    public Task<UserPageRelationship?> GetAsync(UserId userId, PageId pageId, CancellationToken ct = default)
    {
        Gets++;
        return Task.FromResult(memory.FindRelationship(userId, pageId));
    }

    public Task UpdateAsync(UserPageRelationship relationship, CancellationToken ct = default)
    {
        memory.Stage(() => memory.Seed(relationship));
        return Task.CompletedTask;
    }
}

internal sealed class FakeListening(EngagementMemory memory) : IListeningRecordRepository
{
    public int Adds { get; private set; }

    public int LastListedCount { get; private set; }

    public Task AddAsync(ListeningRecord listeningRecord, CancellationToken ct = default)
    {
        Adds++;
        memory.Stage(() => memory.Seed(listeningRecord));
        return Task.CompletedTask;
    }

    public Task<ListeningRecord?> GetAsync(UserId userId, VoiceId voiceId, CancellationToken ct = default) =>
        Task.FromResult(memory.FindListening(userId, voiceId));

    public Task<IReadOnlyList<ListeningRecord>> ListAsync(
        UserId userId,
        IReadOnlyCollection<VoiceId> voiceIds,
        CancellationToken ct = default)
    {
        LastListedCount = voiceIds.Count;
        var ids = voiceIds.Select(id => id.Value).ToHashSet();
        IReadOnlyList<ListeningRecord> matches = memory.ListeningRecords
            .Where(record => record.UserId == userId && ids.Contains(record.VoiceId.Value))
            .ToArray();
        return Task.FromResult(matches);
    }

    public Task UpdateAsync(ListeningRecord listeningRecord, CancellationToken ct = default)
    {
        memory.Stage(() => memory.Seed(listeningRecord));
        return Task.CompletedTask;
    }
}

internal sealed class FakeSessions(EngagementMemory memory) : IPlaySessionRepository
{
    public Task AddAsync(PlaySession session, CancellationToken ct = default)
    {
        memory.Stage(() => memory.Seed(session));
        return Task.CompletedTask;
    }

    public Task<PlaySession?> GetAsync(PlaySessionId playSessionId, CancellationToken ct = default) =>
        Task.FromResult(memory.FindSession(playSessionId));

    public Task UpdateAsync(PlaySession session, CancellationToken ct = default)
    {
        memory.Stage(() => memory.Seed(session));
        return Task.CompletedTask;
    }
}
