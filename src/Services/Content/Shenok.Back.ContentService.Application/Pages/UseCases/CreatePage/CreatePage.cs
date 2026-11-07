using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Pages.UseCases.CreatePage;

public sealed record CreatePageRequest(string Title, string? Description, string? CoverImageUrl);

public sealed class CreatePageValidator : AbstractValidator<CreatePageRequest>
{
    public CreatePageValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(PageTitle.MaxLength);
        RuleFor(x => x.Description).MaximumLength(PageDescription.MaxLength);
        RuleFor(x => x.CoverImageUrl).MaximumLength(CoverImageUrl.MaxLength);
    }
}

public sealed class CreatePageUseCase(IContentCaller caller, IPageRepository pages, IUnitOfWork unitOfWork)
    : IUseCase<CreatePageRequest, PageResponse>
{
    public async Task<PageResponse> ExecuteAsync(CreatePageRequest request, CancellationToken cancellationToken)
    {
        PageAccess.EnsureCanCreate(caller);

        var page = Page.CreateDraft(
            OwnerId.From(caller.UserId),
            PageTitle.Create(request.Title),
            PageDescription.Create(request.Description),
            CoverImageUrl.Create(request.CoverImageUrl));

        await pages.AddAsync(page, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ContentMaps.ToPage(page, []);
    }
}
