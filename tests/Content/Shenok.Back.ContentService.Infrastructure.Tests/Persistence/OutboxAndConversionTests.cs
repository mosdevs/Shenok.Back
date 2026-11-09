using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Infrastructure.Persistence.Conversions;
using Shenok.Back.ContentService.Infrastructure.Persistence.Outbox;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Persistence;

public sealed class OutboxAndConversionTests
{
    [Fact]
    public void Page_events_do_not_become_outbox_rows()
    {
        var page = ContentFactory.DraftPage();

        Assert.Null(OutboxMessage.From(page.DomainEvents.Single()));
    }

    [Fact]
    public void Waveform_register_and_revoke_map_to_the_integration_types()
    {
        var page = ContentFactory.DraftPage();
        var mapping = ContentFactory.PageMapping(page.Id);
        var registeredEvent = mapping.DomainEvents.Single();
        var registered = OutboxMessage.From(registeredEvent);

        Assert.NotNull(registered);
        Assert.Equal(OutboxMessage.RegisteredType, registered.Type);
        Assert.Equal(registeredEvent.EventId, registered.Id);
        Assert.Equal(DateTimeKind.Utc, registered.OccurredOn.Kind);
        Assert.Null(registered.PublishedOn);
        Assert.InRange(registered.Payload.Length, 1, 4096);
        Assert.Contains(mapping.Id.Value.ToString(), registered.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(mapping.WaveformCode.Value.ToString(), registered.Payload, StringComparison.Ordinal);

        mapping.ClearDomainEvents();
        mapping.Revoke();
        var revoked = OutboxMessage.From(mapping.DomainEvents.Single());

        Assert.NotNull(revoked);
        Assert.Equal(OutboxMessage.RevokedType, revoked.Type);
        Assert.DoesNotContain("AccountKey", revoked.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Mark_published_requires_utc_and_rejects_a_second_call()
    {
        var message = OutboxMessage.From(ContentFactory.PageMapping(ContentFactory.DraftPage().Id).DomainEvents.Single());
        Assert.NotNull(message);
        var local = DateTime.SpecifyKind(new DateTime(2026, 11, 9, 9, 0, 0), DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() => message.MarkPublished(local));
        Assert.Null(message.PublishedOn);

        var published = new DateTime(2026, 11, 9, 9, 5, 0, DateTimeKind.Utc);
        message.MarkPublished(published);

        Assert.Equal(published, message.PublishedOn);
        Assert.Throws<InvalidOperationException>(() => message.MarkPublished(published.AddMinutes(1)));
    }

    [Fact]
    public void Voice_order_roundtrips_and_keeps_position()
    {
        var original = new List<VoiceId> { VoiceId.New(), VoiceId.New(), VoiceId.New() };

        var json = Assert.IsType<string>(VoiceIdListConversion.Converter.ConvertToProvider(original));
        var restored = Assert.IsAssignableFrom<IReadOnlyList<VoiceId>>(VoiceIdListConversion.Converter.ConvertFromProvider(json));

        Assert.Equal(original.Select(id => id.Value), restored.Select(id => id.Value));
        Assert.Equal("[]", VoiceIdListConversion.Converter.ConvertToProvider(Array.Empty<VoiceId>()));
    }

    [Fact]
    public void Voice_order_change_is_visible_to_the_comparer()
    {
        var first = VoiceId.New();
        var second = VoiceId.New();
        var left = new List<VoiceId> { first, second };
        var right = new List<VoiceId> { second, first };

        Assert.False(VoiceIdListConversion.Comparer.Equals(left, right));
        Assert.True(VoiceIdListConversion.Comparer.Equals(left, VoiceIdListConversion.Comparer.Snapshot(left)));
    }

    [Fact]
    public void Voice_order_rejects_a_duplicate_or_a_list_past_the_page_limit()
    {
        var id = VoiceId.New();
        Assert.Throws<InvalidOperationException>(() =>
            VoiceIdListConversion.Converter.ConvertToProvider(new List<VoiceId> { id, id }));

        var tooMany = Enumerable.Range(0, AudioLimits.MaxVoicesPerPage + 1).Select(_ => VoiceId.New()).ToList();
        Assert.Throws<InvalidOperationException>(() => VoiceIdListConversion.Converter.ConvertToProvider(tooMany));
    }
}
