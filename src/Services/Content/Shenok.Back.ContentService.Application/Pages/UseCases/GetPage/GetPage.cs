using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Pages.UseCases.GetPage;

public sealed record GetPageRequest(Guid PageId);

public sealed class GetPageValidator : AbstractValidator<GetPageRequest>
{
    public GetPageValidator()
    {
        RuleFor(x => x.PageId).NotEmpty();
    }
}

public sealed class GetPageUseCase(IContentCaller caller, IPageRepository pages, IVoiceRepository voices)
    : IUseCase<GetPageRequest, PageResponse>
{
    public async Task<PageResponse> ExecuteAsync(GetPageRequest request, CancellationToken cancellationToken)
    {
        var page = await ContentMaps.RequirePageAsync(pages, request.PageId, cancellationToken);
        PageAccess.EnsureCanRead(page, caller);
        return await ContentMaps.LoadPageAsync(page, voices, cancellationToken);
    }
}
