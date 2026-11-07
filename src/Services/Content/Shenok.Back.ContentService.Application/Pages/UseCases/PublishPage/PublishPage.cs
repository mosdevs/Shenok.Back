using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
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
    IUnitOfWork unitOfWork) : IUseCase<PublishPageRequest, PageResponse>
{
    public async Task<PageResponse> ExecuteAsync(PublishPageRequest request, CancellationToken cancellationToken)
    {
        var page = await ContentMaps.RequirePageAsync(pages, request.PageId, cancellationToken);
        PageAccess.EnsureCanMutate(page, caller);

        IReadOnlyList<Voice> loaded = page.VoiceIds.Count == 0
            ? []
            : await voices.ListByPageIdsAsync([page.Id], cancellationToken);

        try
        {
            page.Publish(ContentMaps.Readiness(page, loaded));
        }
        catch (PageAlreadyPublishedException exception)
        {
            throw new ConflictException(exception.Message);
        }

        await pages.UpdateAsync(page, cancellationToken);
        await unitOfWork.SaveChanges(cancellationToken);
        return ContentMaps.ToPage(page, loaded);
    }
}
