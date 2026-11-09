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
        Assert.Equal(1, store.Index.ActiveGets);
        Assert.Equal(0, store.Index.Gets);
        Assert.Equal(0, store.Index.Adds);
    }

    [Fact]
    public async Task A_revoked_row_is_the_same_miss()
    {
        var store = new RecognitionFakes();
        store.Seed(Revoked(code: 42));
        var decoder = new FakeDecoder { Result = new WaveformDecodeResult.Success(42) };
        var useCase = UseCase(store, decoder);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(Image(), CancellationToken.None));

        Assert.Equal(DecodeWaveformUseCase.MissDetail, exception.Message);
        Assert.Equal(1, store.Index.ActiveGets);
        Assert.Equal(1, store.Index.Gets);
        Assert.Equal(0, store.Index.Adds);
        Assert.Equal(0, store.UnitOfWork.Attempts);
    }

    [Fact]
    public async Task An_unknown_code_is_the_same_miss()
    {
        var store = new RecognitionFakes();
        var decoder = new FakeDecoder { Result = new WaveformDecodeResult.Success(42) };
        var useCase = UseCase(store, decoder);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(Image(), CancellationToken.None));

        Assert.Equal(DecodeWaveformUseCase.MissDetail, exception.Message);
        Assert.Equal(1, store.Index.ActiveGets);
        Assert.Equal(1, store.Index.Gets);
        Assert.Equal(0, store.Index.Adds);
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
