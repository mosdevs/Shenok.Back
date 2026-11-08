using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

namespace Shenok.Back.EngagementService.Application.Tests;

public class FollowTests
{
    [Fact]
    public async Task Follow_calls_the_subscriber_port_once()
    {
        var harness = new EngagementHarness();

        await harness.Follow().ExecuteAsync(harness.FollowRequest(), CancellationToken.None);
        await harness.Follow().ExecuteAsync(harness.FollowRequest(), CancellationToken.None);

        Assert.Equal([(harness.UserId, harness.PageId, 1)], harness.Counters.SubscriberDeltas);
        Assert.True(Assert.Single(harness.Memory.Relationships).IsFollowing);
        Assert.Equal(1, harness.Memory.Saves);
    }

    [Fact]
    public async Task Unfollow_of_a_missing_relationship_does_not_insert_or_call_the_port()
    {
        var harness = new EngagementHarness();

        await harness.Unfollow().ExecuteAsync(harness.UnfollowRequest(), CancellationToken.None);

        Assert.Empty(harness.Memory.Relationships);
        Assert.Empty(harness.Counters.SubscriberDeltas);
        Assert.Equal(0, harness.Memory.Saves);
    }

    [Fact]
    public async Task Unfollow_after_follow_counts_minus_one_once_and_a_second_unfollow_does_not()
    {
        var harness = new EngagementHarness();
        await harness.RegisterScan().ExecuteAsync(harness.Scan(), CancellationToken.None);
        await harness.Follow().ExecuteAsync(harness.FollowRequest(), CancellationToken.None);

        await harness.Unfollow().ExecuteAsync(harness.UnfollowRequest(), CancellationToken.None);
        await harness.Unfollow().ExecuteAsync(harness.UnfollowRequest(), CancellationToken.None);

        Assert.Equal(
            [(harness.UserId, harness.PageId, 1), (harness.UserId, harness.PageId, -1)],
            harness.Counters.SubscriberDeltas);
        var relationship = Assert.Single(harness.Memory.Relationships);
        Assert.False(relationship.IsFollowing);
        Assert.NotNull(relationship.UnlockedAt);
    }

    [Fact]
    public async Task Follow_reloads_when_the_update_loses_a_concurrency_race()
    {
        var harness = new EngagementHarness();
        var userId = UserId.From(harness.UserId);
        var pageId = PageId.From(harness.PageId);
        harness.Memory.Seed(UserPageRelationship.Start(userId, pageId));
        harness.Memory.Failure = new EngagementConcurrencyException();
        harness.Memory.BeforeConflict = () => harness.Memory.Seed(UserPageRelationship.Start(userId, pageId));

        await harness.Follow().ExecuteAsync(harness.FollowRequest(), CancellationToken.None);

        Assert.Equal([(harness.UserId, harness.PageId, 1)], harness.Counters.SubscriberDeltas);
        Assert.True(harness.Memory.FindRelationship(userId, pageId)!.IsFollowing);
        Assert.Equal(1, harness.Memory.Saves);
    }
}
