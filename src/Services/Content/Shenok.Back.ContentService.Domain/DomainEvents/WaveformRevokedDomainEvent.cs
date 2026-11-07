using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.ContentService.Domain.DomainEvents;

/// <summary>
/// In-process stand-in for the outbox message <c>WaveformRevoked</c>.
/// </summary>
public sealed class WaveformRevokedDomainEvent : DomainEvent
{
    public WaveformId WaveformId { get; }
    public WaveformCode WaveformCode { get; }
    public LayoutVersion LayoutVersion { get; }
    public WaveformTargetType TargetType { get; }
    public Guid TargetId { get; }

    public WaveformRevokedDomainEvent(
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
