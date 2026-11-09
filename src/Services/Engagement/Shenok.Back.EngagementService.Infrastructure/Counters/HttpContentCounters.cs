using Shenok.Back.EngagementService.Application.Common.Interfaces;

namespace Shenok.Back.EngagementService.Infrastructure.Counters;

/// <summary>
/// Delivers a counter command that was stored in the same save as the Engagement fact.
/// </summary>
public sealed class HttpContentCounters(ContentCounterPublisher publisher) : IContentCounters
{
    public Task ApplySubscriberDeltaAsync(
        Guid userId,
        Guid pageId,
        int delta,
        Guid commandId,
        CancellationToken ct = default) =>
        publisher.PublishSubscriberAsync(commandId, userId, pageId, delta, ct);

    public Task DeliverPendingSubscriberAsync(Guid userId, Guid pageId, CancellationToken ct = default) =>
        publisher.DeliverPendingSubscriberAsync(userId, pageId, ct);

    public Task ApplyPlayAsync(string playSessionId, Guid voiceId, Guid commandId, CancellationToken ct = default) =>
        publisher.PublishPlayAsync(commandId, playSessionId, voiceId, ct);

    public Task DeliverPendingPlayAsync(string playSessionId, CancellationToken ct = default) =>
        publisher.DeliverPendingPlayAsync(playSessionId, ct);
}
