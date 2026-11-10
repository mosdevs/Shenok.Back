using Microsoft.Extensions.Logging.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.RecognitionService.Application.Common.Interfaces;
using Shenok.Back.RecognitionService.Application.Recognition.UseCases.DecodeWaveform;
using Shenok.Back.RecognitionService.Domain.Aggregates.WaveformIndex;

namespace Shenok.Back.RecognitionService.Application.Tests;

public class DecodeWaveformUseCaseTests
{
    [Fact]
    public async Task Decoder_failure_is_a_miss_and_does_not_touch_the_index()
    {
        var store = new RecognitionFakes();
        var decoder = new FakeDecoder { Result = new WaveformDecodeResult.Failure(WaveformDecodeResult.AnchorNotFound) };
        var useCase = UseCase(store, decoder);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(Image(), CancellationToken.None));

        Assert.Equal(DecodeWaveformUseCase.MissDetail, exception.Message);
        Assert.Equal(1, decoder.Calls);
        Assert.Equal(0, store.Index.Finds);
        Assert.Equal(0, store.Index.ActiveGets);
        Assert.Equal(0, store.Index.Gets);
        Assert.Equal(0, store.Index.Adds);
        Assert.Equal(0, store.Inbox.Adds);
        Assert.Equal(0, store.UnitOfWork.Attempts);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1L << 40)]
    public async Task An_out_of_range_code_is_the_same_miss_and_does_not_read_the_index(long code)
    {
        var store = new RecognitionFakes();
        var decoder = new FakeDecoder { Result = new WaveformDecodeResult.Success(code) };
        var useCase = UseCase(store, decoder);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(Image(), CancellationToken.None));

        Assert.Equal(DecodeWaveformUseCase.MissDetail, exception.Message);
        Assert.Equal(0, store.Index.Finds);
        Assert.Equal(0, store.Index.ActiveGets);
        Assert.Equal(0, store.Index.Gets);
        Assert.Equal(0, store.Index.Adds);
    }

    [Fact]
    public async Task An_active_row_returns_the_waveform_target_and_nothing_else()
    {
        var waveformId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var store = new RecognitionFakes();
        store.Seed(Active(waveformId, targetId, code: 42));
        var decoder = new FakeDecoder { Result = new WaveformDecodeResult.Success(42) };
        var useCase = UseCase(store, decoder);

        var response = await useCase.ExecuteAsync(Image(), CancellationToken.None);

        Assert.Equal(waveformId, response.WaveformId);
        Assert.Equal(nameof(WaveformTargetType.Page), response.TargetType);
        Assert.Equal(targetId, response.TargetId);
        Assert.Equal(
            [nameof(DecodeWaveformResponse.WaveformId), nameof(DecodeWaveformResponse.TargetType), nameof(DecodeWaveformResponse.TargetId)],
            typeof(DecodeWaveformResponse).GetProperties().Select(property => property.Name).ToArray());
        Assert.Equal(1, store.Index.Finds);
        Assert.Equal(0, store.Index.ActiveGets);
        Assert.Equal(0, store.Index.Gets);
        Assert.Equal(0, store.Index.Adds);
    }

    [Fact]
    public async Task A_revoked_row_is_the_same_miss()
    {
        var store = new RecognitionFakes();
        store.Seed(Revoked(code: 42));
        var logger = new RecordingLogger<DecodeWaveformUseCase>();
        var decoder = new FakeDecoder { Result = new WaveformDecodeResult.Success(42) };
        var useCase = new DecodeWaveformUseCase(decoder, store.Index, logger);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(Image(), CancellationToken.None));

        Assert.Equal(DecodeWaveformUseCase.MissDetail, exception.Message);
        Assert.Equal(1, store.Index.Finds);
        Assert.Equal(0, store.Index.ActiveGets);
        Assert.Equal(0, store.Index.Gets);
        Assert.Equal(0, store.Index.Adds);
        Assert.Equal(0, store.UnitOfWork.Attempts);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("revoked", entry.Reason);
        Assert.Null(entry.Detail);
    }

    [Fact]
    public async Task An_unknown_code_is_the_same_miss()
    {
        var store = new RecognitionFakes();
        var logger = new RecordingLogger<DecodeWaveformUseCase>();
        var decoder = new FakeDecoder { Result = new WaveformDecodeResult.Success(42) };
        var useCase = new DecodeWaveformUseCase(decoder, store.Index, logger);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(Image(), CancellationToken.None));

        Assert.Equal(DecodeWaveformUseCase.MissDetail, exception.Message);
        Assert.Equal(1, store.Index.Finds);
        Assert.Equal(0, store.Index.ActiveGets);
        Assert.Equal(0, store.Index.Gets);
        Assert.Equal(0, store.Index.Adds);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal("unknown_code", entry.Reason);
        Assert.Null(entry.Detail);
    }

    [Fact]
    public async Task A_stable_decoder_reason_is_logged_and_any_other_reason_is_dropped()
    {
        var stable = new RecordingLogger<DecodeWaveformUseCase>();
        var store = new RecognitionFakes();
        var stableDecoder = new FakeDecoder { Result = new WaveformDecodeResult.Failure(WaveformDecodeResult.AnchorNotFound) };
        var stableUseCase = new DecodeWaveformUseCase(stableDecoder, store.Index, stable);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            stableUseCase.ExecuteAsync(Image(), CancellationToken.None));

        var stableEntry = Assert.Single(stable.Entries);
        Assert.Equal("decode_failed", stableEntry.Reason);
        Assert.Equal(WaveformDecodeResult.AnchorNotFound, stableEntry.Detail);

        var hostileReason = "codec failed\r\npassword=secret";
        var hostile = new RecordingLogger<DecodeWaveformUseCase>();
        var hostileDecoder = new FakeDecoder { Result = new WaveformDecodeResult.Failure(hostileReason) };
        var hostileUseCase = new DecodeWaveformUseCase(hostileDecoder, store.Index, hostile);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            hostileUseCase.ExecuteAsync(Image(), CancellationToken.None));

        var hostileEntry = Assert.Single(hostile.Entries);
        Assert.Equal("decode_failed", hostileEntry.Reason);
        Assert.Null(hostileEntry.Detail);
        Assert.DoesNotContain("password", hostileEntry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", hostileEntry.Message, StringComparison.Ordinal);
        Assert.Equal(0, store.Index.Finds);
    }

    [Fact]
    public async Task An_out_of_range_code_logs_decode_failed_without_the_number()
    {
        var logger = new RecordingLogger<DecodeWaveformUseCase>();
        var store = new RecognitionFakes();
        var decoder = new FakeDecoder { Result = new WaveformDecodeResult.Success(0) };
        var useCase = new DecodeWaveformUseCase(decoder, store.Index, logger);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(Image(), CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("decode_failed", entry.Reason);
        Assert.Null(entry.Detail);
        Assert.DoesNotContain("0", entry.Message, StringComparison.Ordinal);
    }

    private static DecodeWaveformUseCase UseCase(RecognitionFakes store, FakeDecoder decoder)
        => new(decoder, store.Index, NullLogger<DecodeWaveformUseCase>.Instance);

    private static DecodeWaveformRequest Image()
        => new("image/jpeg", [1, 2, 3]);

    private static WaveformIndexEntry Active(Guid waveformId, Guid targetId, long code)
        => WaveformIndexEntry.Register(Fact(waveformId, targetId, code));

    private static WaveformIndexEntry Revoked(long code)
        => WaveformIndexEntry.RevokeFirst(Fact(Guid.NewGuid(), Guid.NewGuid(), code));

    private static WaveformFact Fact(Guid waveformId, Guid targetId, long code)
        => WaveformFact.Create(
            WaveformCode.From(code),
            WaveformId.From(waveformId),
            WaveformTargetType.Page,
            targetId,
            LayoutVersion.Version1,
            new DateTime(2026, 11, 9, 10, 15, 30, DateTimeKind.Utc),
            Guid.NewGuid());
}
