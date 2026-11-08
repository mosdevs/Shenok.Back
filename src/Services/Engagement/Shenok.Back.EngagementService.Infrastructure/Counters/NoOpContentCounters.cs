using Shenok.Back.EngagementService.Application.Common.Interfaces;

namespace Shenok.Back.EngagementService.Infrastructure.Counters;

/// <summary>
/// Phase 3 counter port. Completes without calling Content. Phase 6 replaces this registration.
/// </summary>
public sealed class NoOpContentCounters : IContentCounters
{
    public Task ApplySubscriberDeltaAsync(Guid userId, Guid pageId, int delta, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task ApplyPlayAsync(string playSessionId, Guid voiceId, CancellationToken ct = default) =>
        Task.CompletedTask;
}
