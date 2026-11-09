namespace Shenok.Back.EngagementService.Application.Common.Interfaces;

/// <summary>
/// Tells Content that a subscriber count or a play count changed.
/// The command row is written in the same save as the Engagement fact.
/// Call <see cref="ApplySubscriberDeltaAsync"/> or <see cref="ApplyPlayAsync"/> only after that save commits.
/// A later retry sees the domain method return false and calls the pending delivery instead,
/// so a failed Content call is not dropped.
/// </summary>
public interface IContentCounters
{
    /// <summary>
    /// <paramref name="delta"/> is +1 for a new follow and -1 for a new unfollow.
    /// <paramref name="commandId"/> is the domain event id stored with the save.
    /// </summary>
    Task ApplySubscriberDeltaAsync(
        Guid userId,
        Guid pageId,
        int delta,
        Guid commandId,
        CancellationToken ct = default);

    /// <summary>
    /// Sends subscriber commands for this pair that were committed and not acknowledged.
    /// </summary>
    Task DeliverPendingSubscriberAsync(Guid userId, Guid pageId, CancellationToken ct = default);

    /// <summary>
    /// Counts one play. The Content receipt key is <paramref name="playSessionId"/>.
    /// <paramref name="commandId"/> is the domain event id stored with the save.
    /// </summary>
    Task ApplyPlayAsync(string playSessionId, Guid voiceId, Guid commandId, CancellationToken ct = default);

    /// <summary>
    /// Sends a play command for this session that was committed and not acknowledged.
    /// </summary>
    Task DeliverPendingPlayAsync(string playSessionId, CancellationToken ct = default);
}
