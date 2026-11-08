using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.DomainEvents;

namespace Shenok.Back.EngagementService.Application.Tests;

public class RegisterScanTests
{
    [Fact]
    public async Task Second_scan_returns_the_first_snapshot_and_does_not_unlock_again()
    {
        var harness = new EngagementHarness();
        var scans = harness.RegisterScan();

        var first = await scans.ExecuteAsync(harness.Scan(), CancellationToken.None);
        await harness.Follow().ExecuteAsync(harness.FollowRequest(), CancellationToken.None);
        var readsAfterFollow = harness.Relationships.Gets;

        var second = await scans.ExecuteAsync(harness.Scan(), CancellationToken.None);

        Assert.Equal(first, second);
        Assert.False(second.IsFollowing);
        Assert.True(second.PromptFollow);
        Assert.Equal(readsAfterFollow, harness.Relationships.Gets);
        Assert.Equal([(harness.UserId, harness.PageId, 1)], harness.Counters.SubscriberDeltas);
        var relationship = Assert.Single(harness.Memory.Relationships);
        Assert.Single(relationship.DomainEvents.OfType<PageUnlockedDomainEvent>());
    }

    [Fact]
    public async Task Scan_stored_for_another_user_conflicts_without_returning_the_snapshot()
    {
        var harness = new EngagementHarness();
        var winner = ScanReceipt.Record(
            ScanId.From("scan-1"),
            UserId.New(),
            WaveformId.From(harness.WaveformId),
            VoiceId.From(harness.VoiceId),
            PageId.From(harness.PageId),
            unlocked: true,
            isFollowing: true,
            promptFollow: false,
            PlaybackPosition.From(99));
        harness.Memory.Seed(winner);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            harness.RegisterScan().ExecuteAsync(harness.Scan(), CancellationToken.None));

        Assert.Equal(EngagementConflict.ScanBelongsToAnotherUser, exception.Message);
        Assert.DoesNotContain("99", exception.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Counters.SubscriberDeltas);
        Assert.Empty(harness.Memory.Relationships);
    }

    [Fact]
    public async Task Scan_of_a_new_pair_unlocks_without_following_or_counting_a_subscriber()
    {
        var harness = new EngagementHarness();

        var response = await harness.RegisterScan().ExecuteAsync(harness.Scan(), CancellationToken.None);

        Assert.True(response.Unlocked);
        Assert.False(response.IsFollowing);
        Assert.True(response.PromptFollow);
        Assert.Equal(0, response.ResumePositionSeconds);
        Assert.Empty(harness.Counters.SubscriberDeltas);
        Assert.Empty(harness.Memory.ListeningRecords);
        var relationship = Assert.Single(harness.Memory.Relationships);
        Assert.NotNull(relationship.UnlockedAt);
        Assert.False(relationship.IsFollowing);
    }

    [Fact]
    public async Task Auto_follow_follows_once_and_a_later_follow_does_not_count_again()
    {
        var harness = new EngagementHarness();

        var response = await harness.RegisterScan(autoFollow: true).ExecuteAsync(harness.Scan(), CancellationToken.None);
        await harness.RegisterScan(autoFollow: true).ExecuteAsync(harness.Scan(), CancellationToken.None);
        await harness.Follow().ExecuteAsync(harness.FollowRequest(), CancellationToken.None);

        Assert.True(response.Unlocked);
        Assert.True(response.IsFollowing);
        Assert.False(response.PromptFollow);
        Assert.Equal([(harness.UserId, harness.PageId, 1)], harness.Counters.SubscriberDeltas);
    }

    [Fact]
    public async Task Scan_resumes_from_the_listening_record_and_does_not_insert_one()
    {
        var harness = new EngagementHarness();
        var record = ListeningRecord.Start(UserId.From(harness.UserId), VoiceId.From(harness.VoiceId));
        record.ReportProgress(PlaybackPosition.From(42), completed: false, isSeek: false);
        harness.Memory.Seed(record);

        var response = await harness.RegisterScan().ExecuteAsync(harness.Scan(), CancellationToken.None);

        Assert.Equal(42, response.ResumePositionSeconds);
        Assert.Single(harness.Memory.ListeningRecords);
    }

    [Fact]
    public async Task Lost_scan_insert_returns_the_winning_snapshot_for_the_same_user()
    {
        var harness = new EngagementHarness();
        var winner = ScanReceipt.Record(
            ScanId.From("scan-1"),
            UserId.From(harness.UserId),
            WaveformId.From(harness.WaveformId),
            VoiceId.From(harness.VoiceId),
            PageId.From(harness.PageId),
            unlocked: true,
            isFollowing: true,
            promptFollow: false,
            PlaybackPosition.From(7));
        harness.Memory.Failure = new EngagementInsertConflictException(EngagementInsertTarget.ScanReceipt);
        harness.Memory.BeforeConflict = () => harness.Memory.Seed(winner);

        var response = await harness.RegisterScan().ExecuteAsync(harness.Scan(), CancellationToken.None);

        Assert.True(response.Unlocked);
        Assert.True(response.IsFollowing);
        Assert.False(response.PromptFollow);
        Assert.Equal(7, response.ResumePositionSeconds);
        Assert.Empty(harness.Counters.SubscriberDeltas);
        Assert.Empty(harness.Memory.Relationships);
        Assert.Same(winner, Assert.Single(harness.Memory.Receipts));
    }

    [Fact]
    public async Task Lost_scan_insert_for_another_user_conflicts_without_the_snapshot()
    {
        var harness = new EngagementHarness();
        harness.Memory.Failure = new EngagementInsertConflictException(EngagementInsertTarget.ScanReceipt);
        harness.Memory.BeforeConflict = () => harness.Memory.Seed(ScanReceipt.Record(
            ScanId.From("scan-1"),
            UserId.New(),
            WaveformId.From(harness.WaveformId),
            VoiceId.From(harness.VoiceId),
            PageId.From(harness.PageId),
            unlocked: true,
            isFollowing: false,
            promptFollow: true,
            PlaybackPosition.From(99)));

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            harness.RegisterScan().ExecuteAsync(harness.Scan(), CancellationToken.None));

        Assert.Equal(EngagementConflict.ScanBelongsToAnotherUser, exception.Message);
        Assert.DoesNotContain("99", exception.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Memory.Relationships);
        Assert.Empty(harness.Counters.SubscriberDeltas);
    }

    [Fact]
    public async Task Lost_relationship_insert_reloads_and_does_not_unlock_twice()
    {
        var harness = new EngagementHarness();
        var userId = UserId.From(harness.UserId);
        var pageId = PageId.From(harness.PageId);
        harness.Memory.Failure = new EngagementInsertConflictException(EngagementInsertTarget.UserPageRelationship);
        harness.Memory.BeforeConflict = () =>
        {
            var winner = UserPageRelationship.Start(userId, pageId);
            winner.Unlock();
            harness.Memory.Seed(winner);
        };

        var response = await harness.RegisterScan().ExecuteAsync(harness.Scan(), CancellationToken.None);

        Assert.True(response.Unlocked);
        Assert.False(response.IsFollowing);
        Assert.True(response.PromptFollow);
        var relationship = Assert.Single(harness.Memory.Relationships);
        Assert.Single(relationship.DomainEvents.OfType<PageUnlockedDomainEvent>());
        Assert.Single(harness.Memory.Receipts);
        Assert.Empty(harness.Counters.SubscriberDeltas);
    }

    [Fact]
    public async Task Lost_relationship_insert_still_follows_when_auto_follow_is_on()
    {
        var harness = new EngagementHarness();
        var userId = UserId.From(harness.UserId);
        var pageId = PageId.From(harness.PageId);
        harness.Memory.Failure = new EngagementInsertConflictException(EngagementInsertTarget.UserPageRelationship);
        harness.Memory.BeforeConflict = () =>
        {
            var winner = UserPageRelationship.Start(userId, pageId);
            winner.Unlock();
            harness.Memory.Seed(winner);
        };

        var response = await harness.RegisterScan(autoFollow: true)
            .ExecuteAsync(harness.Scan(), CancellationToken.None);

        Assert.True(response.IsFollowing);
        Assert.False(response.PromptFollow);
        Assert.Equal([(harness.UserId, harness.PageId, 1)], harness.Counters.SubscriberDeltas);
        Assert.True(Assert.Single(harness.Memory.Relationships).IsFollowing);
    }

    [Fact]
    public async Task Second_relationship_insert_failure_throws_without_unlocking()
    {
        var harness = new EngagementHarness();
        harness.Memory.Failure = new EngagementInsertConflictException(EngagementInsertTarget.UserPageRelationship);
        harness.Memory.BeforeConflict = () =>
            harness.Memory.Failure = new EngagementInsertConflictException(EngagementInsertTarget.UserPageRelationship);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.RegisterScan().ExecuteAsync(harness.Scan(), CancellationToken.None));

        Assert.Equal("The scan could not be stored.", exception.Message);
        Assert.Empty(harness.Memory.Relationships);
        Assert.Empty(harness.Memory.Receipts);
        Assert.Empty(harness.Counters.SubscriberDeltas);
        Assert.Equal(0, harness.Memory.Saves);
    }
}
