using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Waveforms.UseCases.RegisterWaveform;
using Shenok.Back.ContentService.Application.Waveforms.UseCases.RevokeWaveform;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using WaveformBars = Shenok.Back.WaveformCodec.WaveformCodec;

namespace Shenok.Back.ContentService.Application.Tests;

public class WaveformAccessTests
{
    [Fact]
    public async Task Register_for_an_attached_voice_encodes_that_voice()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var voice = ApplicationScenes.Pending(page);
        page.AddVoiceReference(voice.Id, voice.Status);
        var (pages, voices) = await ApplicationScenes.Store(page, voice);
        var codes = new FakeCodes();
        var useCase = new RegisterWaveformUseCase(owner, pages, voices, new FakeMappings(), codes, new FakeUnitOfWork());

        var registered = await useCase.ExecuteAsync(
            new RegisterWaveformRequest(nameof(WaveformTargetType.Voice), voice.Id.Value),
            CancellationToken.None);

        Assert.Equal(nameof(WaveformTargetType.Voice), registered.TargetType);
        Assert.Equal(voice.Id.Value, registered.TargetId);
        Assert.Equal(1, registered.LayoutVersion);
        Assert.True(WaveformBars.TryDecode(registered.Bars.Select(bar => (byte)bar).ToArray(), out var decoded));
        Assert.Equal((ulong)registered.Code, decoded);
        Assert.Equal(1, codes.Calls);
    }

    [Fact]
    public async Task A_stranger_cannot_register_and_a_bad_target_type_does_not_allocate()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var (pages, voices) = await ApplicationScenes.Store(page);
        var codes = new FakeCodes();
        var stranger = new RegisterWaveformUseCase(
            new FakeCaller { UserId = Guid.NewGuid() },
            pages,
            voices,
            new FakeMappings(),
            codes,
            new FakeUnitOfWork());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            stranger.ExecuteAsync(new RegisterWaveformRequest(nameof(WaveformTargetType.Page), page.Id.Value), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() =>
            new RegisterWaveformUseCase(owner, pages, voices, new FakeMappings(), codes, new FakeUnitOfWork())
                .ExecuteAsync(new RegisterWaveformRequest("0", page.Id.Value), CancellationToken.None));
        Assert.Equal(0, codes.Calls);
    }

    [Fact]
    public async Task Revoke_a_voice_waveform_checks_the_voice_page_owner()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var voice = ApplicationScenes.Pending(page);
        page.AddVoiceReference(voice.Id, voice.Status);
        var mapping = WaveformMapping.RegisterForVoice(WaveformCode.From(15), voice.Id, LayoutVersion.V1, pageIsArchived: false);
        var (pages, voices) = await ApplicationScenes.Store(page, voice);
        var mappings = new FakeMappings();
        await mappings.AddAsync(mapping);
        var unit = new FakeUnitOfWork();
        var useCase = new RevokeWaveformUseCase(owner, pages, voices, mappings, unit);

        var revoked = await useCase.ExecuteAsync(new RevokeWaveformRequest(mapping.Id.Value), CancellationToken.None);

        Assert.Equal(nameof(WaveformMappingStatus.Revoked), revoked.Status);
        Assert.False(mapping.IsActive);
        Assert.Equal(1, unit.Saves);

        var missing = new RevokeWaveformUseCase(owner, pages, voices, mappings, unit);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            missing.ExecuteAsync(new RevokeWaveformRequest(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Register_conflicts_after_three_reserved_codes()
    {
        var owner = new FakeCaller();
        var page = ApplicationScenes.Draft(owner);
        var (pages, voices) = await ApplicationScenes.Store(page);
        var mappings = new FakeMappings { RejectRemaining = 3 };
        var codes = new FakeCodes();
        var unit = new FakeUnitOfWork();
        var useCase = new RegisterWaveformUseCase(owner, pages, voices, mappings, codes, unit);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new RegisterWaveformRequest(nameof(WaveformTargetType.Page), page.Id.Value), CancellationToken.None));

        Assert.Equal("A waveform code could not be allocated.", exception.Message);
        Assert.Equal(3, codes.Calls);
        Assert.Equal(0, mappings.Adds);
        Assert.Equal(0, unit.Saves);
    }
}
