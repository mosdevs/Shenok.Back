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
        Assert.Empty(InstanceMutators(typeof(ScanReceipt)));
    }

    private static IReadOnlyList<string> InstanceMutators(Type type) =>
        type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();
}
