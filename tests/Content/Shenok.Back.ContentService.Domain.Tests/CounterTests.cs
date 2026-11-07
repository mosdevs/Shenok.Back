using Shenok.Back.ContentService.Domain.Aggregates.Counters;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;

namespace Shenok.Back.ContentService.Domain.Tests;

public class CounterTests
{
    [Fact]
    public void PageCounter_starts_at_zero_subscribers()
    {
        var pageId = PageId.New();
        var counter = PageCounter.Create(pageId);

        Assert.Equal(pageId, counter.PageId);
        Assert.Equal(0, counter.SubscriberCount);
    }

    [Fact]
    public void VoiceCounter_starts_at_zero_plays()
    {
        var voiceId = VoiceId.New();
        var counter = VoiceCounter.Create(voiceId);

        Assert.Equal(voiceId, counter.VoiceId);
        Assert.Equal(0, counter.PlayCount);
    }
}
