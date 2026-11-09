using Shenok.Back.RecognitionService.Domain.Exceptions;
using LayoutRevision = Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex.LayoutVersion; // The int property hides this type.

namespace Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

/// <summary>
/// One code in the recognition index. This is a projection of Content's mapping, not an aggregate:
/// it raises no domain events and it does not inherit <c>AggregateRoot</c>.
/// Content owns the mapping. Recognition stores the latest fact it is willing to believe.
/// </summary>
/// <remarks>
/// Timestamps on the row are the event's <c>occurredOn</c>. The methods do not read the clock.
/// The caller normalizes that instant to UTC before it arrives here. <see cref="DateTime"/>
/// compares ticks and ignores <see cref="DateTimeKind"/>, so a local value would compare equal
/// to a UTC value that is a different instant. A non-UTC timestamp is rejected before any compare.
/// A stale fact, a duplicate event, and a conflicting register do not throw. Throwing would
/// nack a message that can never succeed. The inbox still records that event id.
/// </remarks>
public sealed class WaveformIndexEntry
{
    public WaveformCode Code { get; private set; } = null!;

    public WaveformId WaveformId { get; private set; } = null!;

    public WaveformTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public int LayoutVersion { get; private set; }

    public WaveformIndexStatus Status { get; private set; }

    public DateTime SourceOccurredOn { get; private set; }

    public Guid SourceEventId { get; private set; }

    public bool IsActive => Status == WaveformIndexStatus.Active;

    /// <summary>
    /// Parameterless constructor for EF Core materialization. Application code uses the factories.
    /// </summary>
    private WaveformIndexEntry()
    {
    }

    private WaveformIndexEntry(
        WaveformCode code,
        WaveformId waveformId,
        WaveformTargetType targetType,
        Guid targetId,
        int layoutVersion,
        DateTime occurredOn,
        Guid eventId,
        WaveformIndexStatus status)
    {
        Code = code;
        WaveformId = waveformId;
        TargetType = targetType;
        TargetId = targetId;
        LayoutVersion = layoutVersion;
        Status = status;
        SourceOccurredOn = occurredOn;
        SourceEventId = eventId;
    }

    /// <summary>
    /// First fact for this code is a registration. The row is active.
    /// </summary>
    public static WaveformIndexEntry Register(
        WaveformCode code,
        WaveformId waveformId,
        WaveformTargetType targetType,
        Guid targetId,
        int layoutVersion,
        DateTime occurredOn,
        Guid eventId)
        => Create(code, waveformId, targetType, targetId, layoutVersion, occurredOn, eventId, WaveformIndexStatus.Active);

    /// <summary>
    /// First fact for this code is a revoke. The row is a tombstone, so a later register cannot create an active hit.
    /// </summary>
    public static WaveformIndexEntry RevokeFirst(
        WaveformCode code,
        WaveformId waveformId,
        WaveformTargetType targetType,
        Guid targetId,
        int layoutVersion,
        DateTime occurredOn,
        Guid eventId)
        => Create(code, waveformId, targetType, targetId, layoutVersion, occurredOn, eventId, WaveformIndexStatus.Revoked);

    /// <summary>
    /// Applies a register to a row that already exists.
    /// A strictly older timestamp is stale. The same event id at the same timestamp changes nothing.
    /// A revoked row stays revoked, including when the register is newer.
    /// The same waveform and the same target move the source timestamp only when the fact is strictly newer.
    /// A different waveform id or target is a conflict and leaves the row alone.
    /// When the timestamps are equal and the identities differ, the register loses.
    /// </summary>
    public IndexApplyResult ApplyRegistered(
        WaveformCode code,
        WaveformId waveformId,
        WaveformTargetType targetType,
        Guid targetId,
        int layoutVersion,
        DateTime occurredOn,
        Guid eventId)
    {
        EnsureFact(code, waveformId, targetType, targetId, layoutVersion, occurredOn, eventId);
        EnsureSameCode(code);

        if (occurredOn < SourceOccurredOn)
            return IndexApplyResult.IgnoredStale;

        if (occurredOn == SourceOccurredOn && eventId == SourceEventId)
            return IndexApplyResult.Unchanged;

        if (Status == WaveformIndexStatus.Revoked)
            return IndexApplyResult.IgnoredConflict;

        if (!SameIdentity(waveformId, targetType, targetId))
            return IndexApplyResult.IgnoredConflict;

        // Equal timestamps do not refresh the row. Only a strictly newer register of the same identity does.
        if (occurredOn == SourceOccurredOn)
            return IndexApplyResult.Unchanged;

        SourceOccurredOn = occurredOn;
        SourceEventId = eventId;
        return IndexApplyResult.Applied;
    }

    /// <summary>
    /// Applies a revoke to a row that already exists.
    /// A strictly older timestamp is stale. The same event id changes nothing once it is not older.
    /// An equal timestamp with a different event id wins: revoke beats register.
    /// The waveform id, target, and layout version stay as they were. Only status and the source fact move.
    /// </summary>
    public IndexApplyResult ApplyRevoked(
        WaveformCode code,
        WaveformId waveformId,
        WaveformTargetType targetType,
        Guid targetId,
        int layoutVersion,
        DateTime occurredOn,
        Guid eventId)
    {
        EnsureFact(code, waveformId, targetType, targetId, layoutVersion, occurredOn, eventId);
        EnsureSameCode(code);

        if (occurredOn < SourceOccurredOn)
            return IndexApplyResult.IgnoredStale;

        if (eventId == SourceEventId)
            return IndexApplyResult.Unchanged;

        Status = WaveformIndexStatus.Revoked;
        SourceOccurredOn = occurredOn;
        SourceEventId = eventId;
        return IndexApplyResult.Applied;
    }

    private static WaveformIndexEntry Create(
        WaveformCode code,
        WaveformId waveformId,
        WaveformTargetType targetType,
        Guid targetId,
        int layoutVersion,
        DateTime occurredOn,
        Guid eventId,
        WaveformIndexStatus status)
    {
        EnsureFact(code, waveformId, targetType, targetId, layoutVersion, occurredOn, eventId);
        var version = LayoutRevision.From(layoutVersion);

        return new WaveformIndexEntry(
            code,
            waveformId,
            targetType,
            targetId,
            version.Value,
            occurredOn,
            eventId,
            status);
    }

    private void EnsureSameCode(WaveformCode code)
    {
        if (code != Code)
            throw new ArgumentException(
                "A waveform fact can only be applied to the index row for that code.",
                nameof(code));
    }

    private bool SameIdentity(WaveformId waveformId, WaveformTargetType targetType, Guid targetId)
        => waveformId == WaveformId && targetType == TargetType && targetId == TargetId;

    private static void EnsureFact(
        WaveformCode code,
        WaveformId waveformId,
        WaveformTargetType targetType,
        Guid targetId,
        int layoutVersion,
        DateTime occurredOn,
        Guid eventId)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(waveformId);

        if (!Enum.IsDefined(targetType) || targetId == Guid.Empty)
            throw new InvalidWaveformTargetException(targetType, targetId);

        _ = LayoutRevision.From(layoutVersion);

        if (occurredOn.Kind != DateTimeKind.Utc)
            throw new ArgumentException(
                "Occurred-on must be UTC. Normalize the publisher timestamp before it reaches the index.",
                nameof(occurredOn));

        if (eventId == Guid.Empty)
            throw new ArgumentException("Event id cannot be empty.", nameof(eventId));
    }
}
