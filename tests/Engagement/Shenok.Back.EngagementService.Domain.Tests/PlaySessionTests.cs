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
}
