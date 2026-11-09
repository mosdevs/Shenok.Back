namespace Shenok.Back.ContentService.Infrastructure.Persistence.Counters;

/// <summary>
/// One accepted play. The primary key is the play session id.
/// </summary>
public sealed class PlayCounterReceipt
{
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid VoiceId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PlayCounterReceipt()
    {
    }
}
