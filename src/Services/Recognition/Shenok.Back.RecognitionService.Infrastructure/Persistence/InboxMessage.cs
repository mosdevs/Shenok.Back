using Shenok.Back.RecognitionService.Domain.Repositories;

namespace Shenok.Back.RecognitionService.Infrastructure.Persistence;

/// <summary>
/// One consumed waveform event. The payload is not stored. The primary key is the event id,
/// so a redelivery cannot insert a second row.
/// </summary>
public sealed class InboxMessage
{
    public Guid EventId { get; private set; }

    public WaveformMessageType MessageType { get; private set; }

    public DateTime ReceivedOn { get; private set; }

    private InboxMessage()
    {
    }

    public InboxMessage(Guid eventId, WaveformMessageType messageType, DateTime receivedOn)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("Event id cannot be empty.", nameof(eventId));
        if (!Enum.IsDefined(messageType))
            throw new ArgumentException("Message type must be WaveformRegistered or WaveformRevoked.", nameof(messageType));
        if (receivedOn.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Received time must be UTC.", nameof(receivedOn));

        EventId = eventId;
        MessageType = messageType;
        ReceivedOn = receivedOn;
    }
}
