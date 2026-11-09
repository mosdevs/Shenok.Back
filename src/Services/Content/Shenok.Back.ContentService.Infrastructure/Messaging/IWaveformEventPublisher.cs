namespace Shenok.Back.ContentService.Infrastructure.Messaging;

/// <summary>
/// Sends one outbox payload. The body is the stored JSON, not a second serialization.
/// </summary>
internal interface IWaveformEventPublisher
{
    Task PublishAsync(Guid eventId, string messageType, string payload, CancellationToken cancellationToken);
}
