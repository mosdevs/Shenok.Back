using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Voices.UseCases.UpdateVoiceTitle;

public sealed record UpdateVoiceTitleRequest(Guid VoiceId, string Title);

public sealed class UpdateVoiceTitleValidator : AbstractValidator<UpdateVoiceTitleRequest>
{
    public UpdateVoiceTitleValidator()
    {
        RuleFor(x => x.VoiceId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(VoiceTitle.MaxLength);
    }
}

public sealed class UpdateVoiceTitleUseCase(
    IContentCaller caller,
    IVoiceRepository voices,
    IPageRepository pages,
    IUnitOfWork unitOfWork) : IUseCase<UpdateVoiceTitleRequest, VoiceResponse>
{
    public async Task<VoiceResponse> ExecuteAsync(UpdateVoiceTitleRequest request, CancellationToken cancellationToken)
    {
        var voice = await ContentMaps.RequireVoiceAsync(voices, request.VoiceId, cancellationToken);
        var page = await ContentMaps.RequirePageAsync(pages, voice.PageId.Value, cancellationToken);
        PageAccess.EnsureCanMutate(page, caller);
        voice.UpdateTitle(VoiceTitle.Create(request.Title), page.Status == PageStatus.Archived);
        await voices.UpdateAsync(voice, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ContentMaps.ToVoice(voice);
    }
}
