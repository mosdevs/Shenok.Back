using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Domain.Repositories;

namespace Shenok.Back.ContentService.Application.Pages.UseCases.ReorderVoices;

public sealed record ReorderVoicesRequest(Guid PageId, IReadOnlyList<Guid> VoiceIds);

public sealed class ReorderVoicesValidator : AbstractValidator<ReorderVoicesRequest>
{
    public ReorderVoicesValidator()
    {
        RuleFor(x => x.PageId).NotEmpty();
        RuleFor(x => x.VoiceIds)
            .NotNull()
            .Must(ids => ids.Count <= AudioLimits.MaxVoicesPerPage)
            .WithMessage($"A page cannot list more than {AudioLimits.MaxVoicesPerPage} voices.");
        RuleForEach(x => x.VoiceIds).NotEmpty();
    }
}

public sealed class ReorderVoicesUseCase(
    IContentCaller caller,
    IPageRepository pages,
    IUnitOfWork unitOfWork,
    IPageCatalogReader catalog) : IUseCase<ReorderVoicesRequest, PageResponse>
{
    public async Task<PageResponse> ExecuteAsync(ReorderVoicesRequest request, CancellationToken cancellationToken)
    {
        if (request.VoiceIds.Count > AudioLimits.MaxVoicesPerPage)
            throw new ConflictException($"A page cannot list more than {AudioLimits.MaxVoicesPerPage} voices.");

        var page = await ContentMaps.RequirePageAsync(pages, request.PageId, cancellationToken);
        PageAccess.EnsureCanMutate(page, caller);

        var order = new VoiceId[request.VoiceIds.Count];
        for (var i = 0; i < request.VoiceIds.Count; i++)
        {
            if (request.VoiceIds[i] == Guid.Empty)
                throw new NotFoundException("Voice was not found.");

            order[i] = VoiceId.From(request.VoiceIds[i]);
        }

        page.ReorderVoices(order);
        await pages.UpdateAsync(page, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ContentMaps.LoadPageAsync(page, catalog, cancellationToken);
    }
}
