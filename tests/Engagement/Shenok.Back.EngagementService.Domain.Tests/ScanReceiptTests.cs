using System.Reflection;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;

namespace Shenok.Back.EngagementService.Domain.Tests;

public class ScanReceiptTests
{
    [Fact]
    public void Record_keeps_the_snapshot_it_was_given()
    {
        var scanId = ScanId.From("scan-1");
        var userId = UserId.New();
        var waveformId = WaveformId.New();
        var voiceId = VoiceId.New();
        var pageId = PageId.New();
        var resume = PlaybackPosition.From(42);
        var before = DateTime.UtcNow;

        var receipt = ScanReceipt.Record(
            scanId,
            userId,
            waveformId,
            voiceId,
            pageId,
            unlocked: true,
            isFollowing: false,
            promptFollow: true,
            resume);

        var after = DateTime.UtcNow;

        Assert.Equal(scanId, receipt.Id);
        Assert.Equal(userId, receipt.UserId);
        Assert.Equal(waveformId, receipt.WaveformId);
        Assert.Equal(voiceId, receipt.VoiceId);
        Assert.Equal(pageId, receipt.PageId);
        Assert.True(receipt.Unlocked);
        Assert.False(receipt.IsFollowing);
        Assert.True(receipt.PromptFollow);
        Assert.Equal(42, receipt.ResumePosition.Value);
        Assert.InRange(receipt.CreatedAt, before, after);
        Assert.Equal(DateTimeKind.Utc, receipt.CreatedAt.Kind);
        Assert.Empty(receipt.DomainEvents);
        Assert.Equal([nameof(ScanReceipt.BelongsTo)], InstanceMutators(typeof(ScanReceipt)));
        Assert.Empty(PublicSetters(typeof(ScanReceipt)));
    }

    [Fact]
    public void BelongsTo_is_true_only_for_the_user_who_stored_the_receipt()
    {
        var owner = UserId.New();
        var receipt = ScanReceipt.Record(
            ScanId.From("scan-1"),
            owner,
            WaveformId.New(),
            VoiceId.New(),
            PageId.New(),
            unlocked: true,
            isFollowing: false,
            promptFollow: true,
            PlaybackPosition.From(42));

        Assert.True(receipt.BelongsTo(owner));
        Assert.True(receipt.BelongsTo(UserId.From(owner.Value)));
        Assert.False(receipt.BelongsTo(UserId.New()));
        Assert.Throws<ArgumentNullException>(() => receipt.BelongsTo(null!));
        Assert.Empty(receipt.DomainEvents);
    }

    private static IReadOnlyList<string> InstanceMutators(Type type) =>
        type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

    private static IReadOnlyList<string> PublicSetters(Type type) =>
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => property.Name)
            .ToArray();
}
