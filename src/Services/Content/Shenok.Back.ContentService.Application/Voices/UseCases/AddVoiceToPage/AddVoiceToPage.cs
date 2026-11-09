using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Exceptions;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Voices.UseCases.AddVoiceToPage;

public sealed record AddVoiceToPageRequest(Guid PageId, Guid VoiceId);

public sealed class AddVoiceToPageValidator : AbstractValidator<AddVoiceToPageRequest>
{
    public AddVoiceToPageValidator()
    {
        RuleFor(x => x.PageId).NotEmpty();
        RuleFor(x => x.VoiceId).NotEmpty();
    }
}

public sealed class AddVoiceToPageUseCase(
    IContentCaller caller,
    IPageRepository pages,
    IVoiceRepository voices,
    IUnitOfWork unitOfWork,
    IPageCatalogReader catalog) : IUseCase<AddVoiceToPageRequest, PageResponse>
{
    public async Task<PageResponse> ExecuteAsync(AddVoiceToPageRequest request, CancellationToken cancellationToken)
    {
        var page = await ContentMaps.RequirePageAsync(pages, request.PageId, cancellationToken);
        PageAccess.EnsureCanMutate(page, caller);
        if (page.VoiceIds.Count >= AudioLimits.MaxVoicesPerPage)
            throw new ConflictException($"A page cannot have more than {AudioLimits.MaxVoicesPerPage} voices.");

        var voice = await ContentMaps.RequireVoiceAsync(voices, request.VoiceId, cancellationToken);
        if (voice.PageId != page.Id)
            throw new ConflictException("Voice belongs to a different page.");

        try
        {
            page.AddVoiceReference(voice.Id, voice.Status);
        }
        catch (DuplicateVoiceOnPageException exception)
        {
            throw new ConflictException(exception.Message);
        }

        await pages.UpdateAsync(page, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ContentMaps.LoadPageAsync(page, catalog, cancellationToken);
    }
}
