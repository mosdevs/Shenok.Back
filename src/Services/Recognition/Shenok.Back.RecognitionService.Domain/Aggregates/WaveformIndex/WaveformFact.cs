using Shenok.Back.RecognitionService.Domain.Exceptions;

namespace Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

/// <summary>
/// One bus fact about a code. The factories and both apply methods take this single value
/// so a caller cannot swap the target id with the event id.
/// </summary>
/// <remarks>
/// <see cref="Create"/> is the only way to build one. The type is a class: a struct's default
/// value would skip validation and look like an empty code.
/// </remarks>
public sealed record WaveformFact
{
    public WaveformCode Code { get; }

    public WaveformId WaveformId { get; }

    public WaveformTargetType TargetType { get; }

    public Guid TargetId { get; }

    public LayoutVersion Layout { get; }

    public DateTime OccurredOn { get; }

    public Guid EventId { get; }

    private WaveformFact(
        WaveformCode code,
        WaveformId waveformId,
        WaveformTargetType targetType,
        Guid targetId,
        LayoutVersion layout,
        DateTime occurredOn,
        Guid eventId)
    {
        Code = code;
        WaveformId = waveformId;
        TargetType = targetType;
        TargetId = targetId;
        Layout = layout;
        OccurredOn = occurredOn;
        EventId = eventId;
    }

    public static WaveformFact Create(
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
        EnsureTarget(targetType, targetId);

        var layout = LayoutVersion.From(layoutVersion);

        if (occurredOn.Kind != DateTimeKind.Utc)
            throw new ArgumentException(
                "Occurred-on must be UTC. Normalize the publisher timestamp before it reaches the index.",
                nameof(occurredOn));

        if (eventId == Guid.Empty)
            throw new ArgumentException("Event id cannot be empty.", nameof(eventId));

        return new WaveformFact(
            code,
            waveformId,
            targetType,
            targetId,
            layout,
            occurredOn,
            eventId);
    }

    /// <summary>
    /// Page and Voice are the only legal targets. A new enum member has to be added here
    /// before it can be stored. The default arm keeps a corrupt wire value on
    /// <see cref="InvalidWaveformTargetException"/> instead of a switch-expression failure.
    /// </summary>
    private static void EnsureTarget(WaveformTargetType targetType, Guid targetId)
    {
        switch (targetType)
        {
            case WaveformTargetType.Page:
            case WaveformTargetType.Voice:
                break;
            default:
                throw new InvalidWaveformTargetException(targetType, targetId);
        }

        if (targetId == Guid.Empty)
            throw new InvalidWaveformTargetException(targetType, targetId);
    }
}
