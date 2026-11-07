using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.DomainEvents;
using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Domain.Tests;

public class WaveformMappingTests
{
    [Fact]
    public void RegisterForPage_creates_an_active_mapping_and_raises_registered()
    {
        var pageId = PageId.New();
        var mapping = ContentFixtures.ActivePageMapping(pageId, code: 99);

        Assert.True(mapping.IsActive);
        Assert.Equal(WaveformMappingStatus.Active, mapping.Status);
        Assert.Equal(WaveformTargetType.Page, mapping.TargetType);
        Assert.Equal(pageId.Value, mapping.TargetId);
        Assert.Equal(99, mapping.WaveformCode.Value);
        Assert.Equal(LayoutVersion.V1, mapping.LayoutVersion);
        Assert.NotEqual(default, mapping.RegisteredAt);
        mapping.EnsureActiveForPlayback();

        var registered = Assert.Single(mapping.DomainEvents.OfType<WaveformRegisteredDomainEvent>());
        Assert.Equal(mapping.Id, registered.WaveformId);
        Assert.Equal(pageId.Value, registered.TargetId);
    }

    [Fact]
    public void RegisterForVoice_targets_the_voice_id()
    {
        var voiceId = VoiceId.New();

        var mapping = WaveformMapping.RegisterForVoice(
            WaveformCode.From(7),
            voiceId,
            LayoutVersion.V1,
            pageIsArchived: false);

        Assert.Equal(WaveformTargetType.Voice, mapping.TargetType);
        Assert.Equal(voiceId.Value, mapping.TargetId);
        Assert.True(mapping.IsActive);
    }

    [Fact]
    public void Register_rejects_an_archived_page()
    {
        var pageId = PageId.New();

        var ex = Assert.Throws<CannotRegisterWaveformOnArchivedPageException>(() =>
            ContentFixtures.ActivePageMapping(pageId, pageIsArchived: true));

        Assert.Equal(WaveformTargetType.Page, ex.TargetType);
        Assert.Equal(pageId.Value, ex.TargetId);
    }

    [Fact]
    public void Register_rejects_an_empty_target_id()
    {
        Assert.Throws<ArgumentException>(() =>
            WaveformMapping.Register(
                WaveformCode.From(1),
                WaveformTargetType.Page,
                Guid.Empty,
                LayoutVersion.V1,
                targetPageIsArchived: false));
    }

    [Fact]
    public void Many_active_mappings_may_share_a_target()
    {
        var pageId = PageId.New();
        var first = ContentFixtures.ActivePageMapping(pageId, code: 1);
        var second = ContentFixtures.ActivePageMapping(pageId, code: 2);

        Assert.True(first.IsActive);
        Assert.True(second.IsActive);
        Assert.Equal(first.TargetId, second.TargetId);
        Assert.NotEqual(first.WaveformCode, second.WaveformCode);
    }

    [Fact]
    public void Revoke_makes_the_mapping_inactive_and_raises_revoked()
    {
        var mapping = ContentFixtures.ActivePageMapping(PageId.New());

        mapping.Revoke();

        Assert.False(mapping.IsActive);
        Assert.Equal(WaveformMappingStatus.Revoked, mapping.Status);
        Assert.Single(mapping.DomainEvents.OfType<WaveformRevokedDomainEvent>());
        Assert.Throws<WaveformNotActiveException>(() => mapping.EnsureActiveForPlayback());
    }

    [Fact]
    public void Second_revoke_throws()
    {
        var mapping = ContentFixtures.ActivePageMapping(PageId.New());
        mapping.Revoke();

        var ex = Assert.Throws<WaveformAlreadyRevokedException>(() => mapping.Revoke());
        Assert.Equal(mapping.Id, ex.WaveformId);
        Assert.Equal(WaveformMappingStatus.Revoked, mapping.Status);
        Assert.Single(mapping.DomainEvents.OfType<WaveformRevokedDomainEvent>());
    }
}
