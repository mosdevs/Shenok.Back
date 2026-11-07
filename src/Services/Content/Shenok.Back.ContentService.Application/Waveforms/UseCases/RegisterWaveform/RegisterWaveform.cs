using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Exceptions;
using Shenok.Back.ContentService.Domain.Repositories;
using Shenok.Back.ContentService.Domain.Services;
using WaveformBars = Shenok.Back.WaveformCodec.WaveformCodec;

namespace Shenok.Back.ContentService.Application.Waveforms.UseCases.RegisterWaveform;

public sealed record RegisterWaveformRequest(string TargetType, Guid TargetId);

public sealed record RegisterWaveformResponse(
    Guid WaveformId,
    long Code,
    int LayoutVersion,
    string TargetType,
    Guid TargetId,
    IReadOnlyList<int> Bars);

public sealed class RegisterWaveformValidator : AbstractValidator<RegisterWaveformRequest>
{
    public RegisterWaveformValidator()
    {
        RuleFor(x => x.TargetId).NotEmpty();
        RuleFor(x => x.TargetType)
            .Must(type => type is nameof(WaveformTargetType.Page) or nameof(WaveformTargetType.Voice))
            .WithMessage("Target type must be Page or Voice.");
    }
}

public sealed class RegisterWaveformUseCase(
    IContentCaller caller,
    IPageRepository pages,
    IVoiceRepository voices,
    IWaveformMappingRepository mappings,
    IWaveformCodeAllocator codes,
    IUnitOfWork unitOfWork) : IUseCase<RegisterWaveformRequest, RegisterWaveformResponse>
{
    private const int AllocateAttempts = 3;

    public async Task<RegisterWaveformResponse> ExecuteAsync(RegisterWaveformRequest request, CancellationToken cancellationToken)
    {
        if (request.TargetType is not (nameof(WaveformTargetType.Page) or nameof(WaveformTargetType.Voice)))
            throw new ConflictException("Target type must be Page or Voice.");

        var targetType = Enum.Parse<WaveformTargetType>(request.TargetType);
        var (page, voice) = await LoadTargetAsync(targetType, request.TargetId, cancellationToken);
        PageAccess.EnsureCanMutate(page, caller);
        if (page.Status == PageStatus.Archived)
            throw new PageArchivedException(page.Id);
        if (voice is not null && !page.VoiceIds.Contains(voice.Id))
            throw new ConflictException("Voice is not on the page.");

        for (var attempt = 1; attempt <= AllocateAttempts; attempt++)
        {
            var code = await codes.AllocateAsync(cancellationToken);
            if (await mappings.CodeExistsAsync(code, cancellationToken))
                continue;

            var encoded = WaveformBars.Encode(checked((ulong)code.Value));
            if (encoded.Length != WaveformBars.BarCount)
                throw new InvalidOperationException("Waveform codec returned an unexpected layout.");

            var mapping = voice is null
                ? WaveformMapping.RegisterForPage(code, page.Id, LayoutVersion.V1, page.Status == PageStatus.Archived)
                : WaveformMapping.RegisterForVoice(code, voice.Id, LayoutVersion.V1, page.Status == PageStatus.Archived);

            try
            {
                await mappings.AddAsync(mapping, cancellationToken);
                await unitOfWork.SaveChanges(cancellationToken);
                return new RegisterWaveformResponse(
                    mapping.Id.Value,
                    mapping.WaveformCode.Value,
                    mapping.LayoutVersion.Value,
                    mapping.TargetType.ToString(),
                    mapping.TargetId,
                    Heights(encoded));
            }
            catch (WaveformCodeAlreadyUsedException) when (attempt == AllocateAttempts)
            {
                throw new ConflictException("A waveform code could not be allocated.");
            }
            catch (WaveformCodeAlreadyUsedException)
            {
                continue;
            }
        }

        throw new ConflictException("A waveform code could not be allocated.");
    }

    private async Task<(Page Page, Voice? Voice)> LoadTargetAsync(
        WaveformTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        if (targetType == WaveformTargetType.Page)
        {
            var page = await ContentMaps.RequirePageAsync(pages, targetId, cancellationToken);
            return (page, null);
        }

        var voice = await ContentMaps.RequireVoiceAsync(voices, targetId, cancellationToken);
        var ownerPage = await ContentMaps.RequirePageAsync(pages, voice.PageId.Value, cancellationToken);
        return (ownerPage, voice);
    }

    private static int[] Heights(byte[] bars)
    {
        var heights = new int[bars.Length];
        for (var i = 0; i < bars.Length; i++)
            heights[i] = bars[i];

        return heights;
    }
}
