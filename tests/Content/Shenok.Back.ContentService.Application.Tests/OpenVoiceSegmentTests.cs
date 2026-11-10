using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Playback.UseCases.OpenVoiceSegment;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Application.Tests;

public class OpenVoiceSegmentTests
{
    [Theory]
    [InlineData("seg000.aac")]
    [InlineData("hls/seg/seg000.aac")]
    [InlineData("  seg000.aac  ")]
    public async Task Ready_voice_returns_the_object_under_its_own_prefix(string path)
    {
        var (useCase, blobs, voice) = await ReadyScene();
        var payload = new byte[] { 0x47, 1, 2, 3, 4 };
        blobs.Objects["hls/seg/seg000.aac"] = payload;

        var opened = await useCase.ExecuteAsync(new OpenVoiceSegmentRequest(voice.Id.Value, path), CancellationToken.None);

        await using (opened.Content)
        {
            using var buffer = new MemoryStream();
            await opened.Content.CopyToAsync(buffer);
            Assert.Equal(payload, buffer.ToArray());
        }

        Assert.Equal(payload.LongLength, opened.Length);
        Assert.Equal(["hls/seg/seg000.aac"], blobs.Opens);
    }

    [Theory]
    [InlineData("../secret.ts")]
    [InlineData("foo/../secret.ts")]
    [InlineData("seg.ts?x=1")]
    [InlineData("seg.ts#frag")]
    [InlineData("packages/other-voice/seg_00000.ts")]
    [InlineData("hls/other/seg.ts")]
    [InlineData("/secret.ts")]
    [InlineData("a\\b.ts")]
    public async Task A_bad_path_is_rejected_without_calling_storage(string path)
    {
        var (useCase, blobs, voice) = await ReadyScene();

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new OpenVoiceSegmentRequest(voice.Id.Value, path), CancellationToken.None));

        Assert.Equal("Segment path is invalid.", exception.Message);
        Assert.Empty(blobs.Opens);
    }

    [Fact]
    public async Task A_missing_object_is_not_found_after_the_key_is_opened()
    {
        var (useCase, blobs, voice) = await ReadyScene();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new OpenVoiceSegmentRequest(voice.Id.Value, "missing.ts"), CancellationToken.None));

        Assert.Equal("Segment was not found.", exception.Message);
        Assert.Equal(["hls/seg/missing.ts"], blobs.Opens);
    }

    [Fact]
    public async Task A_voice_that_is_not_ready_is_rejected_without_calling_storage()
    {
        var page = ApplicationScenes.Draft(new FakeCaller());
        var pending = ApplicationScenes.Pending(page);
        var failed = ApplicationScenes.Pending(page, "Broken");
        failed.CompletePackagingFailure();
        var blobs = new FakeBlobs();
        var voices = new FakeVoices();
        voices.Add(pending);
        voices.Add(failed);
        var useCase = new OpenVoiceSegmentUseCase(Internal(), voices, blobs);

        await Assert.ThrowsAsync<AudioNotReadyException>(() =>
            useCase.ExecuteAsync(new OpenVoiceSegmentRequest(pending.Id.Value, "seg.ts"), CancellationToken.None));
        await Assert.ThrowsAsync<AudioNotReadyException>(() =>
            useCase.ExecuteAsync(new OpenVoiceSegmentRequest(failed.Id.Value, "seg.ts"), CancellationToken.None));
        Assert.Empty(blobs.Opens);
    }

    [Fact]
    public async Task A_caller_that_is_not_internal_is_forbidden_without_calling_storage()
    {
        var (voices, voice) = await StoredReady();
        var blobs = new FakeBlobs();
        var useCase = new OpenVoiceSegmentUseCase(new FakeCaller(), voices, blobs);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(new OpenVoiceSegmentRequest(voice.Id.Value, "seg.ts"), CancellationToken.None));
        Assert.Empty(blobs.Opens);
    }

    [Fact]
    public async Task An_unknown_voice_is_not_found_without_calling_storage()
    {
        var blobs = new FakeBlobs();
        var useCase = new OpenVoiceSegmentUseCase(Internal(), new FakeVoices(), blobs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new OpenVoiceSegmentRequest(Guid.NewGuid(), "seg.ts"), CancellationToken.None));
        Assert.Empty(blobs.Opens);
    }

    private static async Task<(OpenVoiceSegmentUseCase UseCase, FakeBlobs Blobs, Voice Voice)> ReadyScene()
    {
        var (voices, voice) = await StoredReady();
        var blobs = new FakeBlobs();
        return (new OpenVoiceSegmentUseCase(Internal(), voices, blobs), blobs, voice);
    }

    private static async Task<(FakeVoices Voices, Voice Voice)> StoredReady()
    {
        var page = ApplicationScenes.Draft(new FakeCaller());
        var voice = ApplicationScenes.Ready(page);
        var (_, voices) = await ApplicationScenes.Store(page, voice);
        return (voices, voice);
    }

    private static FakeCaller Internal() => new() { IsInternalService = true };
}
