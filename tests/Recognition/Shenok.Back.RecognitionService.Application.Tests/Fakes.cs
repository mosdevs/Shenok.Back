using Shenok.Back.RecognitionService.Application.Common;
using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;
using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Application.Tests;

internal sealed class RecognitionFakes
{
    private readonly Dictionary<long, WaveformIndexEntry> _rows = new();
    private readonly List<WaveformIndexEntry> _pendingRows = new();
    private readonly Dictionary<Guid, StoredInboxMessage> _inbox = new();
    private readonly List<StoredInboxMessage> _pendingInbox = new();

    public RecognitionFakes()
    {
        Index = new FakeWaveformIndex(this);
        Inbox = new FakeInbox(this);
        UnitOfWork = new FakeUnitOfWork(this);
    }

    public FakeWaveformIndex Index { get; }

    public FakeInbox Inbox { get; }

    public FakeUnitOfWork UnitOfWork { get; }

    public void Seed(WaveformIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_rows.ContainsKey(entry.Code.Value))
            throw new InvalidOperationException("An index row for this code is already stored.");

        _rows[entry.Code.Value] = entry;
    }

    internal void Commit()
    {
        foreach (var row in _pendingRows)
            _rows[row.Code.Value] = row;

        _pendingRows.Clear();

        foreach (var message in _pendingInbox)
            _inbox[message.EventId] = message;

        _pendingInbox.Clear();
    }

    internal void Rollback()
    {
        _pendingRows.Clear();
        _pendingInbox.Clear();
    }

    internal WaveformIndexEntry? Find(WaveformCode code)
    {
        foreach (var pending in _pendingRows)
        {
            if (pending.Code == code)
                return pending;
        }

        return _rows.GetValueOrDefault(code.Value);
    }

    internal void AddPending(WaveformIndexEntry entry)
    {
        if (Find(entry.Code) is not null)
            throw new InvalidOperationException("An index row for this code is already stored.");

        _pendingRows.Add(entry);
    }

    internal void AddPending(StoredInboxMessage message)
    {
        if (_inbox.ContainsKey(message.EventId) || _pendingInbox.Any(pending => pending.EventId == message.EventId))
            throw new InvalidOperationException("An inbox row for this event id is already stored.");

        _pendingInbox.Add(message);
    }

    internal bool InboxContains(Guid eventId) => _inbox.ContainsKey(eventId);

    internal IReadOnlyCollection<StoredInboxMessage> CommittedInbox => _inbox.Values;

    internal int CommittedRows => _rows.Count;

    public WaveformIndexEntry? Peek(long code) => _rows.GetValueOrDefault(code);
}

internal sealed record StoredInboxMessage(Guid EventId, WaveformMessageType MessageType, DateTime ReceivedOn);

internal sealed class FakeWaveformIndex(RecognitionFakes store) : IWaveformIndexRepository
{
    public int Adds { get; private set; }

    public int Gets { get; private set; }

    public int ActiveGets { get; private set; }

    public int Count => store.CommittedRows;

    public Task<WaveformIndexEntry?> GetActiveAsync(WaveformCode code, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(code);
        ActiveGets++;
        var row = store.Find(code);
        return Task.FromResult(row is { IsActive: true } ? row : null);
    }

    public Task<WaveformIndexEntry?> GetAsync(WaveformCode code, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(code);
        Gets++;
        return Task.FromResult(store.Find(code));
    }

    public Task AddAsync(WaveformIndexEntry entry, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(entry);
        Adds++;
        store.AddPending(entry);
        return Task.CompletedTask;
    }
}

internal sealed class FakeInbox(RecognitionFakes store) : IInbox
{
    public int Adds { get; private set; }

    public int Count => store.CommittedInbox.Count;

    public IReadOnlyCollection<StoredInboxMessage> Messages => store.CommittedInbox;

    public Task<bool> ExistsAsync(Guid eventId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(store.InboxContains(eventId));
    }

    public Task AddAsync(Guid eventId, WaveformMessageType messageType, DateTime receivedOn, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Adds++;
        store.AddPending(new StoredInboxMessage(eventId, messageType, receivedOn));
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnitOfWork(RecognitionFakes store) : IUnitOfWork
{
    public int Saves { get; private set; }

    public int Attempts { get; private set; }

    public Exception? Failure { get; set; }

    public int RemainingConcurrencyFailures { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Attempts++;

        if (RemainingConcurrencyFailures > 0)
        {
            RemainingConcurrencyFailures--;
            store.Rollback();
            throw new IndexConcurrencyException();
        }

        if (Failure is not null)
        {
            store.Rollback();
            throw Failure;
        }

        store.Commit();
        Saves++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeDecoder : IWaveformDecoder
{
    public WaveformDecodeResult Result { get; set; } = new WaveformDecodeResult.Success(42);

    public int Calls { get; private set; }

    public ValueTask<WaveformDecodeResult> DecodeAsync(ReadOnlyMemory<byte> image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return ValueTask.FromResult(Result);
    }
}
