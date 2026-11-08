using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.DomainEvents;

namespace Shenok.Back.EngagementService.Domain.Tests;

public class PlaySessionTests
{
    [Fact]
    public void AcceptFirstReport_counts_once_and_raises_VoicePlayedDomainEvent_once()
    {
        var id = PlaySessionId.From("session-1");
        var userId = UserId.New();
        var voiceId = VoiceId.New();
        var session = PlaySession.Open(id, userId, voiceId);

        Assert.False(session.Counted);
        Assert.Equal(id, session.Id);
        Assert.Empty(session.DomainEvents);

        Assert.True(session.AcceptFirstReport());

        Assert.True(session.Counted);
        var played = Assert.Single(session.DomainEvents.OfType<VoicePlayedDomainEvent>());
        Assert.Equal(id, played.PlaySessionId);
        Assert.Equal(userId, played.UserId);
        Assert.Equal(voiceId, played.VoiceId);

        Assert.False(session.AcceptFirstReport());

        Assert.True(session.Counted);
        Assert.Single(session.DomainEvents.OfType<VoicePlayedDomainEvent>());
    }

    [Fact]
    public void BelongsTo_requires_the_same_user_and_voice_and_does_not_count_the_session()
    {
        var userId = UserId.New();
        var voiceId = VoiceId.New();
        var session = PlaySession.Open(PlaySessionId.From("session-1"), userId, voiceId);

        Assert.True(session.BelongsTo(userId, voiceId));
        Assert.True(session.BelongsTo(UserId.From(userId.Value), VoiceId.From(voiceId.Value)));
        Assert.False(session.BelongsTo(UserId.New(), voiceId));
        Assert.False(session.BelongsTo(userId, VoiceId.New()));
        Assert.Throws<ArgumentNullException>(() => session.BelongsTo(null!, voiceId));
        Assert.Throws<ArgumentNullException>(() => session.BelongsTo(userId, null!));
        Assert.False(session.Counted);
        Assert.Empty(session.DomainEvents);
    }
}
