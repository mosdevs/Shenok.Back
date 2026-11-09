using FluentValidation;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Counters;
using Shenok.Back.ContentService.Application.Counters.UseCases.ApplySubscriberDelta;
using Shenok.Back.ContentService.Application.Counters.UseCases.IncrementPlayCount;
using Shenok.Back.ContentService.Application.Pages.UseCases.ArchivePage;
using Shenok.Back.ContentService.Application.Pages.UseCases.CreatePage;
using Shenok.Back.ContentService.Application.Pages.UseCases.GetPage;
using Shenok.Back.ContentService.Application.Pages.UseCases.PublishPage;
using Shenok.Back.ContentService.Application.Pages.UseCases.ReorderVoices;
using Shenok.Back.ContentService.Application.Pages.UseCases.UpdatePageMetadata;
using Shenok.Back.ContentService.Application.Playback.UseCases.IssuePlaylist;
using Shenok.Back.ContentService.Application.Voices.UseCases.AddVoiceToPage;
using Shenok.Back.ContentService.Application.Voices.UseCases.GetVoice;
using Shenok.Back.ContentService.Application.Voices.UseCases.PackageVoice;
using Shenok.Back.ContentService.Application.Voices.UseCases.UpdateVoiceTitle;
using Shenok.Back.ContentService.Application.Voices.UseCases.UploadVoice;
using Shenok.Back.ContentService.Application.Waveforms.UseCases.RegisterWaveform;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;

namespace Shenok.Back.ContentService.Application.Tests;

public class CounterAndValidatorTests
{
    [Fact]
    public async Task Unfollow_and_play_commands_are_accepted_then_left_unchanged()
    {
        var caller = new FakeCaller { IsInternalService = true };
        var page = Page.CreateDraft(OwnerId.New(), PageTitle.Create("Show"), PageDescription.Create(null), null);
        var voice = Voice.CreatePending(page.Id, VoiceTitle.Create("Episode"), "sources/a", "audio/wav", 10);
        var pages = new FakePages();
        var voiceStore = new FakeVoices();
        await pages.AddAsync(page);
        voiceStore.Add(voice);
        var subscribers = new ApplySubscriberDeltaUseCase(caller, pages);
        var plays = new IncrementPlayCountUseCase(caller, voiceStore);

        await Assert.ThrowsAsync<CounterUpdateDeferredException>(() =>
            subscribers.ExecuteAsync(
                new ApplySubscriberDeltaRequest(page.Id.Value, -1, $"{Guid.NewGuid():D}:{Guid.NewGuid():D}"),
                CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            subscribers.ExecuteAsync(
                new ApplySubscriberDeltaRequest(Guid.NewGuid(), -1, $"{Guid.NewGuid():D}:{Guid.NewGuid():D}"),
                CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() =>
            subscribers.ExecuteAsync(new ApplySubscriberDeltaRequest(page.Id.Value, -1, "bad key"), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            plays.ExecuteAsync(new IncrementPlayCountRequest(voice.Id.Value, Guid.Empty), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            plays.ExecuteAsync(new IncrementPlayCountRequest(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new IncrementPlayCountUseCase(new FakeCaller(), voiceStore).ExecuteAsync(
                new IncrementPlayCountRequest(voice.Id.Value, Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public void Validators_reject_empty_ids_bad_audio_and_an_oversized_order()
    {
        Assert.False(new CreatePageValidator().Validate(new CreatePageRequest("  ", null, null)).IsValid);
        Assert.False(new UploadVoiceValidator().Validate(new UploadVoiceRequest(Guid.NewGuid(), "Episode", "text/html", 32, new MemoryStream([1]))).IsValid);
        Assert.False(new UploadVoiceValidator().Validate(new UploadVoiceRequest(Guid.NewGuid(), "Episode", "audio/wav", 0, new MemoryStream([1]))).IsValid);
        Assert.False(new UploadVoiceValidator().Validate(new UploadVoiceRequest(Guid.NewGuid(), "Episode", "audio/wav", AudioAsset.MaxSourceSizeBytes + 1, new MemoryStream([1]))).IsValid);
        Assert.True(new UploadVoiceValidator().Validate(new UploadVoiceRequest(Guid.NewGuid(), "Episode", "audio/wav; charset=binary", 32, new MemoryStream(new byte[32]))).IsValid);

        Assert.False(new RegisterWaveformValidator().Validate(new RegisterWaveformRequest("page", Guid.NewGuid())).IsValid);
        Assert.True(new RegisterWaveformValidator().Validate(new RegisterWaveformRequest(nameof(WaveformTargetType.Page), Guid.NewGuid())).IsValid);

        Assert.False(new IssuePlaylistValidator().Validate(new IssuePlaylistRequest(Guid.Empty, null)).IsValid);
        Assert.False(new IssuePlaylistValidator().Validate(new IssuePlaylistRequest(Guid.NewGuid(), Guid.Empty)).IsValid);
        Assert.True(new IssuePlaylistValidator().Validate(new IssuePlaylistRequest(Guid.NewGuid(), null)).IsValid);

        Assert.False(new ApplySubscriberDeltaValidator().Validate(new ApplySubscriberDeltaRequest(Guid.NewGuid(), 5, "user:page")).IsValid);
        Assert.False(new IncrementPlayCountValidator().Validate(new IncrementPlayCountRequest(Guid.Empty, Guid.NewGuid())).IsValid);

        var tooMany = Enumerable.Range(0, AudioLimits.MaxVoicesPerPage + 1).Select(_ => Guid.NewGuid()).ToArray();
        Assert.False(new ReorderVoicesValidator().Validate(new ReorderVoicesRequest(Guid.NewGuid(), tooMany)).IsValid);

        Assert.False(new PublishPageValidator().Validate(new PublishPageRequest(Guid.Empty)).IsValid);
        Assert.False(new ArchivePageValidator().Validate(new ArchivePageRequest(Guid.Empty)).IsValid);
        Assert.False(new GetPageValidator().Validate(new GetPageRequest(Guid.Empty)).IsValid);
        Assert.False(new GetVoiceValidator().Validate(new GetVoiceRequest(Guid.Empty)).IsValid);
        Assert.False(new PackageVoiceValidator().Validate(new PackageVoiceRequest(Guid.Empty)).IsValid);
        Assert.False(new AddVoiceToPageValidator().Validate(new AddVoiceToPageRequest(Guid.Empty, Guid.NewGuid())).IsValid);
        Assert.False(new UpdateVoiceTitleValidator().Validate(new UpdateVoiceTitleRequest(Guid.NewGuid(), "  ")).IsValid);
        Assert.False(new UpdatePageMetadataValidator().Validate(new UpdatePageMetadataRequest(Guid.Empty, "Title", null, null)).IsValid);
        Assert.False(new UpdatePageMetadataValidator().Validate(new UpdatePageMetadataRequest(Guid.NewGuid(), "  ", null, null)).IsValid);
        Assert.True(new PublishPageValidator().Validate(new PublishPageRequest(Guid.NewGuid())).IsValid);
        Assert.True(new GetPageValidator().Validate(new GetPageRequest(Guid.NewGuid())).IsValid);
    }
}
