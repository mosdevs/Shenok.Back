namespace Shenok.Back.ContentService.Infrastructure.Persistence.Counters;

/// <summary>
/// One accepted subscriber command. The primary key is the idempotency key from Engagement.
/// </summary>
public sealed class SubscriberCounterReceipt
{
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid PageId { get; private set; }
    public int Delta { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SubscriberCounterReceipt()
    {
    }
}
