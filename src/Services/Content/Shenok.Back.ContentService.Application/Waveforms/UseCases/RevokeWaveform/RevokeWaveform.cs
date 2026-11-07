using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;
using Shenok.Back.ContentService.Domain.Exceptions;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Waveforms.UseCases.RevokeWaveform;

public sealed record RevokeWaveformRequest(Guid WaveformId);

public sealed record RevokeWaveformResponse(Guid WaveformId, string Status);

public sealed class RevokeWaveformValidator : AbstractValidator<RevokeWaveformRequest>
{
    public RevokeWaveformValidator()
    {
        RuleFor(x => x.WaveformId).NotEmpty();
    }
}

public sealed class RevokeWaveformUseCase(
    IContentCaller caller,
    IPageRepository pages,
    IVoiceRepository voices,
    IWaveformMappingRepository mappings,
    IUnitOfWork unitOfWork) : IUseCase<RevokeWaveformRequest, RevokeWaveformResponse>
{
    public async Task<RevokeWaveformResponse> ExecuteAsync(RevokeWaveformRequest request, CancellationToken cancellationToken)
    {
        var mapping = await ContentMaps.RequireWaveformAsync(mappings, request.WaveformId, cancellationToken);
        var pageId = await ResolvePageIdAsync(mapping, cancellationToken);
        var page = await ContentMaps.RequirePageAsync(pages, pageId, cancellationToken);
        PageAccess.EnsureCanMutate(page, caller);

        try
        {
            mapping.Revoke();
        }
        catch (WaveformAlreadyRevokedException exception)
        {
            throw new ConflictException(exception.Message);
        }

        await mappings.UpdateAsync(mapping, cancellationToken);
        await unitOfWork.SaveChanges(cancellationToken);
        return new RevokeWaveformResponse(mapping.Id.Value, mapping.Status.ToString());
    }

    private async Task<Guid> ResolvePageIdAsync(WaveformMapping mapping, CancellationToken cancellationToken)
    {
        if (mapping.TargetType == WaveformTargetType.Page)
            return mapping.TargetId;

        var voice = await ContentMaps.RequireVoiceAsync(voices, mapping.TargetId, cancellationToken);
        return voice.PageId.Value;
    }
}
