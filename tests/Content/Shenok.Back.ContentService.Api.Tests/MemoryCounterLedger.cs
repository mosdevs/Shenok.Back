using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common.Interfaces;

namespace Shenok.Back.ContentService.Api.Tests;

internal sealed class MemoryCounterLedger : ICounterLedger
{
    private readonly Dictionary<string, (Guid Target, int Delta)> _receipts = [];
    private readonly Dictionary<Guid, long> _subscribers = [];
    private readonly Dictionary<Guid, long> _plays = [];

    public Task<SubscriberCounterUpdate> ApplySubscriberDeltaAsync(
        Guid pageId,
        int delta,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (_receipts.TryGetValue(idempotencyKey, out var existing))
        {
            if (existing.Target != pageId || existing.Delta != delta)
                throw new ConflictException("Idempotency key is already used.");

            return Task.FromResult(new SubscriberCounterUpdate(true, false, _subscribers.GetValueOrDefault(pageId)));
        }

        var count = _subscribers.GetValueOrDefault(pageId);
        if (count + delta >= 0)
        {
            count += delta;
            _subscribers[pageId] = count;
        }

        _receipts[idempotencyKey] = (pageId, delta);
        return Task.FromResult(new SubscriberCounterUpdate(true, true, count));
    }

    public Task<PlayCounterUpdate> IncrementPlayAsync(
        Guid voiceId,
        string playSessionId,
        CancellationToken cancellationToken = default)
    {
        if (_receipts.TryGetValue(playSessionId, out var existing))
        {
            if (existing.Target != voiceId)
                throw new ConflictException("Idempotency key is already used.");

            return Task.FromResult(new PlayCounterUpdate(true, false, _plays.GetValueOrDefault(voiceId)));
        }

        var count = _plays.GetValueOrDefault(voiceId) + 1;
        _plays[voiceId] = count;
        _receipts[playSessionId] = (voiceId, 1);
        return Task.FromResult(new PlayCounterUpdate(true, true, count));
    }

    internal long SubscriberCountOf(Guid pageId) => _subscribers.GetValueOrDefault(pageId);

    internal long PlayCountOf(Guid voiceId) => _plays.GetValueOrDefault(voiceId);
}
