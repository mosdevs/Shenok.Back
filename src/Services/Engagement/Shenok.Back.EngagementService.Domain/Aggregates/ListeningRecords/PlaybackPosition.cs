using Shenok.Back.EngagementService.Domain.Exceptions;

namespace Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;

/// <summary>
/// Playback offset in whole seconds. Engagement does not know the voice duration, so there is no upper bound.
/// </summary>
public sealed record PlaybackPosition
{
    public static PlaybackPosition Zero { get; } = new(0);

    public int Value { get; private set; }

    private PlaybackPosition()
    {
    }

    private PlaybackPosition(int value) => Value = value;

    public static PlaybackPosition From(int seconds)
    {
        if (seconds < 0)
            throw new NegativePlaybackPositionException(seconds);

        return new PlaybackPosition(seconds);
    }

    public override string ToString() => Value.ToString();
}
