namespace Shenok.Back.EngagementService.Application.Common.Interfaces;

/// <summary>
/// Tells Content that a subscriber count or a play count changed.
/// The phase 6 implementation is idempotent. This phase's implementation completes without doing work and cannot fail.
/// Call it only after the Engagement save has succeeded, and only when the domain method returned true.
/// </summary>
public interface IContentCounters
{
    /// <summary>
    /// <paramref name="delta"/> is +1 for a new follow and -1 for a new unfollow.
    /// The idempotency key is the user and page pair, so a retry of the same follow does not add a second subscriber.
    /// </summary>
    Task ApplySubscriberDeltaAsync(Guid userId, Guid pageId, int delta, CancellationToken ct = default);

    /// <summary>
    /// Counts one play. The idempotency key is <paramref name="playSessionId"/>.
    /// </summary>
    Task ApplyPlayAsync(string playSessionId, Guid voiceId, CancellationToken ct = default);
}
