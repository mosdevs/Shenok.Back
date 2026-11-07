using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Voices.UseCases.GetVoice;

public sealed record GetVoiceRequest(Guid VoiceId);

public sealed class GetVoiceValidator : AbstractValidator<GetVoiceRequest>
{
    public GetVoiceValidator()
    {
        RuleFor(x => x.VoiceId).NotEmpty();
    }
}

public sealed class GetVoiceUseCase(IContentCaller caller, IVoiceRepository voices, IPageRepository pages)
    : IUseCase<GetVoiceRequest, VoiceResponse>
{
    public async Task<VoiceResponse> ExecuteAsync(GetVoiceRequest request, CancellationToken cancellationToken)
    {
        var voice = await ContentMaps.RequireVoiceAsync(voices, request.VoiceId, cancellationToken);
        var page = await ContentMaps.RequirePageAsync(pages, voice.PageId.Value, cancellationToken);
        PageAccess.EnsureCanRead(page, caller);
        return ContentMaps.ToVoice(voice);
    }
}
