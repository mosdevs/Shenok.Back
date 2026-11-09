namespace Shenok.Back.ContentService.Application.Common.Interfaces;

/// <summary>
/// Applies one counter change with a receipt. <see cref="SubscriberCounterUpdate.Applied"/> is true
/// only when this call inserted the receipt. A duplicate key returns the current count and does not add again.
/// </summary>
public interface ICounterLedger
{
    Task<SubscriberCounterUpdate> ApplySubscriberDeltaAsync(
        Guid pageId,
        int delta,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<PlayCounterUpdate> IncrementPlayAsync(
        Guid voiceId,
        string playSessionId,
        CancellationToken cancellationToken = default);
}

public readonly record struct SubscriberCounterUpdate(bool Found, bool Applied, long SubscriberCount);

public readonly record struct PlayCounterUpdate(bool Found, bool Applied, long PlayCount);
