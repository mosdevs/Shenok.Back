using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Exceptions;

namespace Shenok.Back.EngagementService.Domain.Tests;

public class IdentifierTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void ScanId_and_PlaySessionId_reject_empty_or_whitespace(string? value)
    {
        Assert.Throws<InvalidScanIdException>(() => ScanId.From(value!));
        Assert.Throws<InvalidPlaySessionIdException>(() => PlaySessionId.From(value!));
    }

    [Fact]
    public void ScanId_and_PlaySessionId_trim_and_reject_a_65_character_string()
    {
        Assert.Throws<InvalidScanIdException>(() => ScanId.From(new string('s', ScanId.MaxLength + 1)));
        Assert.Throws<InvalidPlaySessionIdException>(() => PlaySessionId.From(new string('p', PlaySessionId.MaxLength + 1)));

        Assert.Equal("scan-1", ScanId.From("  scan-1  ").Value);
        Assert.Equal(ScanId.MaxLength, ScanId.From(new string('s', ScanId.MaxLength)).Value.Length);
        Assert.Equal(ScanId.MaxLength, ScanId.From(" " + new string('s', ScanId.MaxLength) + " ").Value.Length);

        Assert.Equal("play-1", PlaySessionId.From("  play-1  ").Value);
        Assert.Equal(PlaySessionId.MaxLength, PlaySessionId.From(new string('p', PlaySessionId.MaxLength)).Value.Length);
        Assert.Equal(PlaySessionId.MaxLength, PlaySessionId.From(" " + new string('p', PlaySessionId.MaxLength) + " ").Value.Length);
    }

    [Fact]
    public void PlaybackPosition_rejects_a_negative_value_and_accepts_zero()
    {
        var ex = Assert.Throws<NegativePlaybackPositionException>(() => PlaybackPosition.From(-1));
        Assert.Equal(-1, ex.Value);
        Assert.Equal(0, PlaybackPosition.From(0).Value);
        Assert.Equal(PlaybackPosition.Zero, PlaybackPosition.From(0));
        Assert.Equal(int.MaxValue, PlaybackPosition.From(int.MaxValue).Value);
    }

    [Fact]
    public void Guid_ids_reject_empty_and_composite_ids_equate_by_pair()
    {
        Assert.Throws<ArgumentException>(() => UserId.From(Guid.Empty));
        Assert.Throws<ArgumentException>(() => PageId.From(Guid.Empty));
        Assert.Throws<ArgumentException>(() => VoiceId.From(Guid.Empty));
        Assert.Throws<ArgumentException>(() => WaveformId.From(Guid.Empty));

        var userGuid = Guid.NewGuid();
        var pageGuid = Guid.NewGuid();
        var voiceGuid = Guid.NewGuid();
        Assert.Equal(userGuid, UserId.From(userGuid).Value);
        Assert.NotEqual(Guid.Empty, UserId.New().Value);
        Assert.NotEqual(Guid.Empty, PageId.New().Value);
        Assert.NotEqual(Guid.Empty, VoiceId.New().Value);
        Assert.NotEqual(Guid.Empty, WaveformId.New().Value);

        var userId = UserId.From(userGuid);
        var pageId = PageId.From(pageGuid);
        var voiceId = VoiceId.From(voiceGuid);
        Assert.Equal(
            new UserPageRelationshipId(userId, pageId),
            new UserPageRelationshipId(UserId.From(userGuid), PageId.From(pageGuid)));
        Assert.NotEqual(
            new UserPageRelationshipId(userId, pageId),
            new UserPageRelationshipId(UserId.New(), pageId));
        Assert.Equal(
            new ListeningRecordId(userId, voiceId),
            new ListeningRecordId(UserId.From(userGuid), VoiceId.From(voiceGuid)));
        Assert.NotEqual(
            new ListeningRecordId(userId, voiceId),
            new ListeningRecordId(userId, VoiceId.New()));
    }
}
