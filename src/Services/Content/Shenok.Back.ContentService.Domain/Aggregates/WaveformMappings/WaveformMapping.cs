using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.DomainEvents;
using Shenok.Back.ContentService.Domain.Exceptions;
using Shenok.Back.SharedKernel;

namespace Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;

/// <summary>
/// One printed code pointed at a page or a voice. Many active mappings may share a target.
/// Register and revoke raise in-process events. The outbox publisher is wired in a later phase.
/// </summary>
public sealed class WaveformMapping : AggregateRoot<WaveformId>
{
    public WaveformCode WaveformCode { get; private set; } = null!;
    public WaveformTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public LayoutVersion LayoutVersion { get; private set; } = null!;
    public DateTime RegisteredAt { get; private set; }
    public WaveformMappingStatus Status { get; private set; }
    public bool IsActive => Status == WaveformMappingStatus.Active;

    private WaveformMapping()
    {
    }

    private WaveformMapping(
        WaveformId id,
        WaveformCode code,
        WaveformTargetType targetType,
        Guid targetId,
        LayoutVersion layoutVersion,
        DateTime registeredAt) : base(id)
    {
        WaveformCode = code;
        TargetType = targetType;
        TargetId = targetId;
        LayoutVersion = layoutVersion;
        RegisteredAt = registeredAt;
        Status = WaveformMappingStatus.Active;
    }

    /// <summary>
    /// The application must load the target page and pass its real archived flag. This aggregate does not load <see cref="Page"/>.
    /// </summary>
    public static WaveformMapping Register(
        WaveformCode code,
        WaveformTargetType targetType,
        Guid targetId,
        LayoutVersion layoutVersion,
        bool targetPageIsArchived)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(layoutVersion);
        if (!Enum.IsDefined(targetType))
            throw new ArgumentOutOfRangeException(nameof(targetType), targetType, "Target type is not defined.");
        if (targetId == Guid.Empty)
            throw new ArgumentException("Target id cannot be empty.", nameof(targetId));
        if (targetPageIsArchived)
            throw new CannotRegisterWaveformOnArchivedPageException(targetType, targetId);

        var mapping = new WaveformMapping(WaveformId.New(), code, targetType, targetId, layoutVersion, DateTime.UtcNow);
        mapping.RaiseDomainEvent(new WaveformRegisteredDomainEvent(
            mapping.Id,
            mapping.WaveformCode,
            mapping.LayoutVersion,
            mapping.TargetType,
            mapping.TargetId));
        return mapping;
    }

    public static WaveformMapping RegisterForPage(
        WaveformCode code,
        PageId pageId,
        LayoutVersion layoutVersion,
        bool pageIsArchived)
    {
        ArgumentNullException.ThrowIfNull(pageId);
        return Register(code, WaveformTargetType.Page, pageId.Value, layoutVersion, pageIsArchived);
    }

    public static WaveformMapping RegisterForVoice(
        WaveformCode code,
        VoiceId voiceId,
        LayoutVersion layoutVersion,
        bool pageIsArchived)
    {
        ArgumentNullException.ThrowIfNull(voiceId);
        return Register(code, WaveformTargetType.Voice, voiceId.Value, layoutVersion, pageIsArchived);
    }

    public void Revoke()
    {
        if (Status == WaveformMappingStatus.Revoked)
            throw new WaveformAlreadyRevokedException(Id);

        Status = WaveformMappingStatus.Revoked;
        RaiseDomainEvent(new WaveformRevokedDomainEvent(Id, WaveformCode, LayoutVersion, TargetType, TargetId));
    }

    public void EnsureActiveForPlayback()
    {
        if (Status != WaveformMappingStatus.Active)
            throw new WaveformNotActiveException(Id);
    }
}
