using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Pages.UseCases.UpdatePageMetadata;

public sealed record UpdatePageMetadataRequest(Guid PageId, string Title, string? Description, string? CoverImageUrl);

public sealed class UpdatePageMetadataValidator : AbstractValidator<UpdatePageMetadataRequest>
{
    public UpdatePageMetadataValidator()
    {
        RuleFor(x => x.PageId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(PageTitle.MaxLength);
        RuleFor(x => x.Description).MaximumLength(PageDescription.MaxLength);
        RuleFor(x => x.CoverImageUrl).MaximumLength(CoverImageUrl.MaxLength);
    }
}

public sealed class UpdatePageMetadataUseCase(
    IContentCaller caller,
    IPageRepository pages,
    IUnitOfWork unitOfWork,
    IPageCatalogReader catalog) : IUseCase<UpdatePageMetadataRequest, PageResponse>
{
    public async Task<PageResponse> ExecuteAsync(UpdatePageMetadataRequest request, CancellationToken cancellationToken)
    {
        var page = await ContentMaps.RequirePageAsync(pages, request.PageId, cancellationToken);
        PageAccess.EnsureCanMutate(page, caller);
        page.UpdateMetadata(
            PageTitle.Create(request.Title),
            PageDescription.Create(request.Description),
            CoverImageUrl.Create(request.CoverImageUrl));

        await pages.UpdateAsync(page, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ContentMaps.LoadPageAsync(page, catalog, cancellationToken);
    }
}
