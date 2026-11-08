namespace Shenok.Back.EngagementService.Application.Common.Interfaces;

/// <summary>
/// Tells Content that a subscriber count or a play count changed.
/// This phase's implementation completes without doing work and cannot fail.
/// Call it only after the Engagement save that observed the domain method return true.
/// A later retry of that follow, unfollow, or play sees the domain method return false and does not call this port again.
/// Phase 6 must write the command in the same transaction as that save before this port can fail.
/// </summary>
public interface IContentCounters
{
    /// <summary>
    /// <paramref name="delta"/> is +1 for a new follow and -1 for a new unfollow.
    /// The idempotency key is the user and page pair. The use case does not send the same delta twice.
    /// </summary>
    Task ApplySubscriberDeltaAsync(Guid userId, Guid pageId, int delta, CancellationToken ct = default);

    /// <summary>
    /// Counts one play. The idempotency key is <paramref name="playSessionId"/>.
    /// </summary>
    Task ApplyPlayAsync(string playSessionId, Guid voiceId, CancellationToken ct = default);
}
