using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Exceptions;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Pages.UseCases.PublishPage;

public sealed record PublishPageRequest(Guid PageId);

public sealed class PublishPageValidator : AbstractValidator<PublishPageRequest>
{
    public PublishPageValidator()
    {
        RuleFor(x => x.PageId).NotEmpty();
    }
}

public sealed class PublishPageUseCase(
    IContentCaller caller,
    IPageRepository pages,
    IVoiceRepository voices,
    IUnitOfWork unitOfWork,
    IPageCatalogReader catalog) : IUseCase<PublishPageRequest, PageResponse>
{
    public async Task<PageResponse> ExecuteAsync(PublishPageRequest request, CancellationToken cancellationToken)
    {
        var page = await ContentMaps.RequirePageAsync(pages, request.PageId, cancellationToken);
        PageAccess.EnsureCanMutate(page, caller);

        var loaded = await voices.ListByPageIdsAsync([page.Id], cancellationToken);
        if (page.VoiceIds.Count == 0 && loaded.Count > 0)
            throw new VoicesNotInEpisodeOrderException(page.Id);

        try
        {
            page.Publish(ContentMaps.Readiness(page, loaded));
        }
        catch (PageAlreadyPublishedException exception)
        {
            throw new ConflictException(exception.Message);
        }

        await pages.UpdateAsync(page, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ContentMaps.LoadPageAsync(page, catalog, cancellationToken);
    }
}
