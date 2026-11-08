using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.DomainEvents;

namespace Shenok.Back.EngagementService.Domain.Tests;

public class UserPageRelationshipTests
{
    private static readonly DateTime Sentinel = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public void Unlock_twice_keeps_the_original_UnlockedAt_and_raises_once()
    {
        var userId = UserId.New();
        var pageId = PageId.New();
        var relationship = UserPageRelationship.Start(userId, pageId);

        Assert.False(relationship.IsFollowing);
        Assert.Null(relationship.FollowedAt);
        Assert.Null(relationship.UnlockedAt);
        Assert.Empty(relationship.DomainEvents);
        Assert.Equal(new UserPageRelationshipId(userId, pageId), relationship.Id);

        Assert.True(relationship.Unlock());
        AggregateAccess.SetProperty(relationship, nameof(UserPageRelationship.UnlockedAt), Sentinel);

        Assert.False(relationship.Unlock());

        Assert.Equal(Sentinel, relationship.UnlockedAt);
        var unlocked = Assert.Single(relationship.DomainEvents.OfType<PageUnlockedDomainEvent>());
        Assert.Equal(userId, unlocked.UserId);
        Assert.Equal(pageId, unlocked.PageId);
    }

    [Fact]
    public void Follow_then_Unfollow_leaves_UnlockedAt_set()
    {
        var relationship = UserPageRelationship.Start(UserId.New(), PageId.New());
        Assert.True(relationship.Unlock());
        AggregateAccess.SetProperty(relationship, nameof(UserPageRelationship.UnlockedAt), Sentinel);

        Assert.True(relationship.Follow());
        Assert.True(relationship.Unfollow());

        Assert.Equal(Sentinel, relationship.UnlockedAt);
        Assert.False(relationship.IsFollowing);
        Assert.Null(relationship.FollowedAt);
    }

    [Fact]
    public void Follow_on_an_unlocked_relationship_keeps_UnlockedAt()
    {
        var relationship = UserPageRelationship.Start(UserId.New(), PageId.New());
        Assert.True(relationship.Unlock());
        AggregateAccess.SetProperty(relationship, nameof(UserPageRelationship.UnlockedAt), Sentinel);

        Assert.True(relationship.Follow());

        Assert.True(relationship.IsFollowing);
        Assert.NotNull(relationship.FollowedAt);
        Assert.Equal(Sentinel, relationship.UnlockedAt);
        Assert.Single(relationship.DomainEvents.OfType<PageFollowedDomainEvent>());
    }

    [Fact]
    public void Unlock_on_a_followed_relationship_keeps_IsFollowing()
    {
        var userId = UserId.New();
        var pageId = PageId.New();
        var relationship = UserPageRelationship.Start(userId, pageId);

        Assert.True(relationship.Follow());
        Assert.True(relationship.Unlock());

        Assert.True(relationship.IsFollowing);
        Assert.NotNull(relationship.FollowedAt);
        Assert.NotNull(relationship.UnlockedAt);
        Assert.Equal(DateTimeKind.Utc, relationship.UnlockedAt.Value.Kind);
        var unlocked = Assert.Single(relationship.DomainEvents.OfType<PageUnlockedDomainEvent>());
        Assert.Equal(userId, unlocked.UserId);
        Assert.Equal(pageId, unlocked.PageId);
        Assert.Single(relationship.DomainEvents.OfType<PageFollowedDomainEvent>());
    }

    [Fact]
    public void Follow_twice_raises_PageFollowedDomainEvent_once()
    {
        var relationship = UserPageRelationship.Start(UserId.New(), PageId.New());

        Assert.True(relationship.Follow());
        AggregateAccess.SetProperty(relationship, nameof(UserPageRelationship.FollowedAt), Sentinel);

        Assert.False(relationship.Follow());

        Assert.True(relationship.IsFollowing);
        Assert.Equal(Sentinel, relationship.FollowedAt);
        Assert.Single(relationship.DomainEvents.OfType<PageFollowedDomainEvent>());
    }

    [Fact]
    public void Unfollow_when_not_following_returns_false_and_adds_no_event()
    {
        var relationship = UserPageRelationship.Start(UserId.New(), PageId.New());

        Assert.False(relationship.Unfollow());

        Assert.False(relationship.IsFollowing);
        Assert.Null(relationship.FollowedAt);
        Assert.Empty(relationship.DomainEvents);
    }

    [Fact]
    public void Unfollow_clears_IsFollowing_and_FollowedAt()
    {
        var userId = UserId.New();
        var pageId = PageId.New();
        var relationship = UserPageRelationship.Start(userId, pageId);
        Assert.True(relationship.Follow());

        Assert.True(relationship.Unfollow());

        Assert.False(relationship.IsFollowing);
        Assert.Null(relationship.FollowedAt);
        var unfollowed = Assert.Single(relationship.DomainEvents.OfType<PageUnfollowedDomainEvent>());
        Assert.Equal(userId, unfollowed.UserId);
        Assert.Equal(pageId, unfollowed.PageId);
        Assert.False(relationship.Unfollow());
        Assert.Single(relationship.DomainEvents.OfType<PageUnfollowedDomainEvent>());
    }

    [Fact]
    public void Follow_after_Unfollow_follows_again()
    {
        var relationship = UserPageRelationship.Start(UserId.New(), PageId.New());
        Assert.True(relationship.Follow());
        Assert.True(relationship.Unfollow());

        Assert.True(relationship.Follow());

        Assert.True(relationship.IsFollowing);
        Assert.NotNull(relationship.FollowedAt);
        Assert.Equal(2, relationship.DomainEvents.OfType<PageFollowedDomainEvent>().Count());
        Assert.Single(relationship.DomainEvents.OfType<PageUnfollowedDomainEvent>());
    }

    [Fact]
    public void Id_is_rebuilt_from_the_user_and_page_columns()
    {
        var relationship = UserPageRelationship.Start(UserId.New(), PageId.New());
        var userId = UserId.New();
        var pageId = PageId.New();
        AggregateAccess.SetProperty(relationship, nameof(UserPageRelationship.UserId), userId);
        AggregateAccess.SetProperty(relationship, nameof(UserPageRelationship.PageId), pageId);

        Assert.Equal(new UserPageRelationshipId(userId, pageId), relationship.Id);
    }
}
