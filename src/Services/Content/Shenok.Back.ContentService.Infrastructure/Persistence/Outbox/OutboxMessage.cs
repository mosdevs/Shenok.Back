using System.Text.Json;
using Shenok.Back.ContentService.Domain.DomainEvents;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Outbox;

/// <summary>
/// Waveform integration row written in the same transaction as the mapping change.
/// The publisher that marks <see cref="PublishedOn"/> is phase 5.
/// </summary>
public sealed class OutboxMessage
{
    public const string RegisteredType = "WaveformRegistered";
    public const string RevokedType = "WaveformRevoked";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredOn { get; private set; }
    public DateTime? PublishedOn { get; private set; }

    private OutboxMessage()
    {
    }

    private OutboxMessage(Guid id, string type, string payload, DateTime occurredOn)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredOn = occurredOn;
    }

    public static OutboxMessage? From(IDomainEvent domainEvent)
    {
        return domainEvent switch
        {
            WaveformRegisteredDomainEvent registered => Create(
                registered.EventId,
                RegisteredType,
                registered.OccuredOn,
                registered.WaveformId.Value,
                registered.WaveformCode.Value,
                registered.LayoutVersion.Value,
                registered.TargetType.ToString(),
                registered.TargetId),
            WaveformRevokedDomainEvent revoked => Create(
                revoked.EventId,
                RevokedType,
                revoked.OccuredOn,
                revoked.WaveformId.Value,
                revoked.WaveformCode.Value,
                revoked.LayoutVersion.Value,
                revoked.TargetType.ToString(),
                revoked.TargetId),
            _ => null
        };
    }

    private static OutboxMessage Create(
        Guid eventId,
        string type,
        DateTime occurredOn,
        Guid waveformId,
        long waveformCode,
        int layoutVersion,
        string targetType,
        Guid targetId)
    {
        var payload = JsonSerializer.Serialize(new WaveformOutboxPayload(
            eventId,
            AsUtc(occurredOn),
            waveformId,
            waveformCode,
            layoutVersion,
            targetType,
            targetId), Json);
        if (payload.Length is 0 or > 4096)
            throw new InvalidOperationException("Waveform outbox payload is invalid.");

        return new OutboxMessage(eventId, type, payload, AsUtc(occurredOn));
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private sealed record WaveformOutboxPayload(
        Guid EventId,
        DateTime OccurredOn,
        Guid WaveformId,
        long WaveformCode,
        int LayoutVersion,
        string TargetType,
        Guid TargetId);
}
