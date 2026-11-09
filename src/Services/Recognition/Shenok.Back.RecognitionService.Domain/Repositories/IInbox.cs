namespace Shenok.Back.RecognitionService.Domain.Repositories;

/// <summary>
/// Records a consumed bus event. The inbox stores the event id, the message type, and when it was received.
/// It does not store the payload. A duplicate event id is how redelivery becomes an acknowledgement.
/// </summary>
public interface IInbox
{
    Task<bool> ExistsAsync(Guid eventId, CancellationToken ct = default);

    Task AddAsync(Guid eventId, WaveformMessageType messageType, DateTime receivedOn, CancellationToken ct = default);
}
