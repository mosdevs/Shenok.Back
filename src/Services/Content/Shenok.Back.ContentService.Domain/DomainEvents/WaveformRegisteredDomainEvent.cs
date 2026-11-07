using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.ContentService.Domain.DomainEvents;

/// <summary>
/// In-process stand-in for the outbox message <c>WaveformRegistered</c>.
/// Payload matches the integration event: id, code, layout version, target type, and target id.
/// </summary>
public sealed class WaveformRegisteredDomainEvent : DomainEvent
{
    public WaveformId WaveformId { get; }
    public WaveformCode WaveformCode { get; }
    public LayoutVersion LayoutVersion { get; }
    public WaveformTargetType TargetType { get; }
    public Guid TargetId { get; }

    public WaveformRegisteredDomainEvent(
        WaveformId waveformId,
        WaveformCode waveformCode,
        LayoutVersion layoutVersion,
        WaveformTargetType targetType,
        Guid targetId)
    {
        WaveformId = waveformId;
        WaveformCode = waveformCode;
        LayoutVersion = layoutVersion;
        TargetType = targetType;
        TargetId = targetId;
    }
}
