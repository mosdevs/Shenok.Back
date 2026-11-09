using Shenok.Back.EngagementService.Domain.DomainEvents;
using Shenok.Back.SharedKernel.DomainEvents;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Counters;

/// <summary>
/// Counter command written in the same save as the follow, unfollow, or play.
/// Content applies it once. <see cref="IdempotencyKey"/> is the receipt key.
/// </summary>
public sealed class CounterCommand
{
    public const string SubscriberKind = "Subscriber";
    public const string PlayKind = "Play";

    public Guid Id { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Guid? UserId { get; private set; }
    public Guid? PageId { get; private set; }
    public Guid? VoiceId { get; private set; }
    public string? PlaySessionId { get; private set; }
    public int Delta { get; private set; }
    public DateTime OccurredOn { get; private set; }
    public DateTime? PublishedOn { get; private set; }

    private CounterCommand()
    {
    }

    public static CounterCommand? From(IDomainEvent domainEvent) => domainEvent switch
    {
        PageFollowedDomainEvent followed => Subscriber(
            followed.EventId,
            followed.UserId.Value,
            followed.PageId.Value,
            delta: 1,
            followed.OccuredOn),
        PageUnfollowedDomainEvent unfollowed => Subscriber(
            unfollowed.EventId,
            unfollowed.UserId.Value,
            unfollowed.PageId.Value,
            delta: -1,
            unfollowed.OccuredOn),
        VoicePlayedDomainEvent played => Play(
            played.EventId,
            played.PlaySessionId.Value,
            played.VoiceId.Value,
            played.OccuredOn),
        _ => null
    };

    public static string SubscriberKey(Guid userId, Guid pageId, Guid eventId) =>
        $"{userId:D}:{pageId:D}:{eventId:D}";

    private static CounterCommand Subscriber(Guid eventId, Guid userId, Guid pageId, int delta, DateTime occurredOn) =>
        new()
        {
            Id = eventId,
            Kind = SubscriberKind,
            IdempotencyKey = SubscriberKey(userId, pageId, eventId),
            UserId = userId,
            PageId = pageId,
            Delta = delta,
            OccurredOn = AsUtc(occurredOn)
        };

    private static CounterCommand Play(Guid eventId, string playSessionId, Guid voiceId, DateTime occurredOn) =>
        new()
        {
            Id = eventId,
            Kind = PlayKind,
            IdempotencyKey = playSessionId,
            VoiceId = voiceId,
            PlaySessionId = playSessionId,
            Delta = 1,
            OccurredOn = AsUtc(occurredOn)
        };

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
