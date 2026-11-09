namespace Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

/// <summary>
/// One code in the recognition index. This is a projection of Content's mapping.
/// It raises no domain events. Content owns the mapping. Recognition stores the latest fact it is willing to believe.
/// </summary>
/// <remarks>
/// Timestamps on the row are the event's <c>occurredOn</c>. The methods do not read the clock.
/// The caller normalizes that instant to UTC before <see cref="WaveformFact.Create"/>.
/// <see cref="DateTime"/> compares ticks and ignores <see cref="DateTimeKind"/>, so a local value
/// would compare equal to a UTC value that is a different instant.
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
    public static WaveformIndexEntry Register(WaveformFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return Create(fact, WaveformIndexStatus.Active);
    }

    /// <summary>
    /// First fact for this code is a revoke. The row is a tombstone, so a later register cannot create an active hit.
    /// </summary>
    public static WaveformIndexEntry RevokeFirst(WaveformFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return Create(fact, WaveformIndexStatus.Revoked);
    }

    /// <summary>
    /// Applies a register to a row that already exists.
    /// A strictly older timestamp is stale, including when the event id matches.
    /// The same event id at an equal or newer timestamp changes nothing. A replay must not move the clock.
    /// A revoked row stays revoked.
    /// A different waveform id or target is a conflict.
    /// The same identity at the same timestamp changes nothing.
    /// The same identity at a strictly newer timestamp refreshes the source fact and the layout version.
    /// </summary>
    public IndexApplyResult ApplyRegistered(WaveformFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        EnsureSameCode(fact);

        if (fact.OccurredOn < SourceOccurredOn)
            return IndexApplyResult.IgnoredStale;

        if (fact.EventId == SourceEventId)
            return IndexApplyResult.Unchanged;

        if (Status == WaveformIndexStatus.Revoked)
            return IndexApplyResult.IgnoredConflict;

        if (!SameIdentity(fact))
            return IndexApplyResult.IgnoredConflict;

        if (fact.OccurredOn == SourceOccurredOn)
            return IndexApplyResult.Unchanged;

        SourceOccurredOn = fact.OccurredOn;
        SourceEventId = fact.EventId;
        LayoutVersion = fact.Layout.Value;
        return IndexApplyResult.Applied;
    }

    /// <summary>
    /// Applies a revoke to a row that already exists.
    /// A strictly older timestamp is stale. The same event id changes nothing once it is not older.
    /// An equal timestamp with a different event id wins: revoke beats register.
    /// Waveform id, target, and layout version stay as they were. Only status and the source fact move.
    /// </summary>
    public IndexApplyResult ApplyRevoked(WaveformFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        EnsureSameCode(fact);

        if (fact.OccurredOn < SourceOccurredOn)
            return IndexApplyResult.IgnoredStale;

        if (fact.EventId == SourceEventId)
            return IndexApplyResult.Unchanged;

        Status = WaveformIndexStatus.Revoked;
        SourceOccurredOn = fact.OccurredOn;
        SourceEventId = fact.EventId;
        return IndexApplyResult.Applied;
    }

    private static WaveformIndexEntry Create(WaveformFact fact, WaveformIndexStatus status)
        => new(
            fact.Code,
            fact.WaveformId,
            fact.TargetType,
            fact.TargetId,
            fact.Layout.Value,
            fact.OccurredOn,
            fact.EventId,
            status);

    private void EnsureSameCode(WaveformFact fact)
    {
        if (fact.Code != Code)
            throw new ArgumentException(
                "A waveform fact can only be applied to the index row for that code.",
                nameof(fact));
    }

    private bool SameIdentity(WaveformFact fact)
        => fact.WaveformId == WaveformId && fact.TargetType == TargetType && fact.TargetId == TargetId;
}
