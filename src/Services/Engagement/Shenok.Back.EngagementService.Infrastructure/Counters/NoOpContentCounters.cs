using Shenok.Back.EngagementService.Application.Common.Interfaces;

namespace Shenok.Back.EngagementService.Infrastructure.Counters;

/// <summary>
/// Test double for hosts that do not call Content. Production registers <see cref="HttpContentCounters"/>.
/// </summary>
public sealed class NoOpContentCounters : IContentCounters
{
    public Task ApplySubscriberDeltaAsync(
        Guid userId,
        Guid pageId,
        int delta,
        Guid commandId,
        CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task DeliverPendingSubscriberAsync(Guid userId, Guid pageId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task ApplyPlayAsync(string playSessionId, Guid voiceId, Guid commandId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task DeliverPendingPlayAsync(string playSessionId, CancellationToken ct = default) =>
        Task.CompletedTask;
}
