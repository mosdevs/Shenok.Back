using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Pages.UseCases.ArchivePage;

public sealed record ArchivePageRequest(Guid PageId);

public sealed class ArchivePageValidator : AbstractValidator<ArchivePageRequest>
{
    public ArchivePageValidator()
    {
        RuleFor(x => x.PageId).NotEmpty();
    }
}

public sealed class ArchivePageUseCase(
    IContentCaller caller,
    IPageRepository pages,
    IVoiceRepository voices,
    IUnitOfWork unitOfWork) : IUseCase<ArchivePageRequest, PageResponse>
{
    public async Task<PageResponse> ExecuteAsync(ArchivePageRequest request, CancellationToken cancellationToken)
    {
        var page = await ContentMaps.RequirePageAsync(pages, request.PageId, cancellationToken);
        PageAccess.EnsureCanMutate(page, caller);
        page.Archive();
        await pages.UpdateAsync(page, cancellationToken);
        await unitOfWork.SaveChanges(cancellationToken);
        return await ContentMaps.LoadPageAsync(page, voices, cancellationToken);
    }
}
