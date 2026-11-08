using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.DomainEvents;
using Shenok.Back.EngagementService.Domain.Repositories;
using Shenok.Back.EngagementService.Infrastructure.Persistence;

namespace Shenok.Back.EngagementService.Infrastructure.Tests;

[Collection(InfrastructureFixture.Name)]
public sealed class DomainEventDispatchTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Each_domain_event_is_dispatched_once_after_commit_and_then_cleared()
    {
        fixture.Events.Clear();
        var userId = UserId.New();
        var pageId = PageId.New();
        var voiceId = VoiceId.New();
        var sessionId = PlaySessionId.From("play-" + Guid.NewGuid().ToString("N"));

        await using (var scope = fixture.CreateScope())
        {
            var relationships = scope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
            var records = scope.ServiceProvider.GetRequiredService<IListeningRecordRepository>();
            var sessions = scope.ServiceProvider.GetRequiredService<IPlaySessionRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var relationship = UserPageRelationship.Start(userId, pageId);
            Assert.True(relationship.Unlock());
            Assert.True(relationship.Follow());
            await relationships.AddAsync(relationship, CancellationToken.None);

            var record = ListeningRecord.Start(userId, voiceId);
            Assert.True(record.ReportProgress(PlaybackPosition.From(3), completed: true, isSeek: false).NewlyCompleted);
            await records.AddAsync(record, CancellationToken.None);

            var session = PlaySession.Open(sessionId, userId, voiceId);
            Assert.True(session.AcceptFirstReport());
            await sessions.AddAsync(session, CancellationToken.None);

            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            Assert.Empty(relationship.DomainEvents);
            Assert.Empty(record.DomainEvents);
            Assert.Empty(session.DomainEvents);

            Assert.True(relationship.Unfollow());
            await relationships.UpdateAsync(relationship, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            Assert.Empty(relationship.DomainEvents);
        }

        Assert.Single(fixture.Events.Events.OfType<PageUnlockedDomainEvent>());
        Assert.Single(fixture.Events.Events.OfType<PageFollowedDomainEvent>());
        Assert.Single(fixture.Events.Events.OfType<PageUnfollowedDomainEvent>());
        Assert.Single(fixture.Events.Events.OfType<VoicePlayedDomainEvent>());
        Assert.Single(fixture.Events.Events.OfType<VoiceCompletedDomainEvent>());
    }

    [Fact]
    public async Task Handler_runs_only_after_the_follow_is_visible_to_another_connection()
    {
        fixture.Events.Clear();
        var userId = UserId.New();
        var pageId = PageId.New();
        var seenCommitted = false;
        fixture.Events.BeforeHandle = domainEvent =>
        {
            if (domainEvent is not PageFollowedDomainEvent followed || followed.PageId != pageId)
                return;

            using var other = fixture.CreateBareContext();
            seenCommitted = other.UserPageRelationships.AsNoTracking()
                .Any(relationship => relationship.UserId == userId && relationship.PageId == pageId && relationship.IsFollowing);
        };

        try
        {
            await using var scope = fixture.CreateScope();
            var relationships = scope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var relationship = UserPageRelationship.Start(userId, pageId);
            Assert.True(relationship.Follow());
            await relationships.AddAsync(relationship, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            fixture.Events.BeforeHandle = null;
        }

        Assert.True(seenCommitted);
        Assert.Contains(
            fixture.Events.Events,
            domainEvent => domainEvent is PageFollowedDomainEvent followed && followed.PageId == pageId);
    }

    [Fact]
    public async Task Handler_failure_leaves_the_committed_unlock()
    {
        fixture.Events.Clear();
        fixture.Events.ThrowOnPageUnlocked = true;
        var userId = UserId.New();
        var pageId = PageId.New();

        try
        {
            await using var scope = fixture.CreateScope();
            var relationships = scope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var relationship = UserPageRelationship.Start(userId, pageId);
            Assert.True(relationship.Unlock());
            await relationships.AddAsync(relationship, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            Assert.Empty(relationship.DomainEvents);
        }
        finally
        {
            fixture.Events.ThrowOnPageUnlocked = false;
        }

        Assert.Contains(fixture.Events.Events, domainEvent => domainEvent is PageUnlockedDomainEvent);
        await using var read = fixture.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<EngagementDbContext>();
        var stored = await db.UserPageRelationships.AsNoTracking()
            .SingleAsync(relationship => relationship.UserId == userId && relationship.PageId == pageId);
        Assert.NotNull(stored.UnlockedAt);
    }

    [Fact]
    public async Task Failed_save_does_not_dispatch()
    {
        fixture.Events.Clear();
        var userId = UserId.New();
        var pageId = PageId.New();

        await using (var scope = fixture.CreateScope())
        {
            var relationships = scope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await relationships.AddAsync(UserPageRelationship.Start(userId, pageId), CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = fixture.CreateScope())
        {
            var relationships = scope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var duplicate = UserPageRelationship.Start(userId, pageId);
            Assert.True(duplicate.Follow());
            await relationships.AddAsync(duplicate, CancellationToken.None);
            await Assert.ThrowsAsync<EngagementInsertConflictException>(() =>
                unitOfWork.SaveChangesAsync(CancellationToken.None));
        }

        Assert.DoesNotContain(
            fixture.Events.Events,
            domainEvent => domainEvent is PageFollowedDomainEvent followed && followed.PageId == pageId);

        await using var read = fixture.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<EngagementDbContext>();
        var stored = await db.UserPageRelationships.AsNoTracking()
            .SingleAsync(relationship => relationship.UserId == userId && relationship.PageId == pageId);
        Assert.False(stored.IsFollowing);
    }
}
