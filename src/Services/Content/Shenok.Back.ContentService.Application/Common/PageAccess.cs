using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Domain.Aggregates.Pages;
using Shenok.Back.ContentService.Domain.Exceptions;

namespace Shenok.Back.ContentService.Application.Common;

internal static class PageAccess
{
    public static void EnsureCanCreate(IContentCaller caller)
    {
        RequireUser(caller);
        if (caller.IsAdmin || caller.IsContentCreator)
            return;

        throw new ForbiddenException("Only a content creator or an admin can create a page.");
    }

    public static void EnsureCanMutate(Page page, IContentCaller caller)
    {
        RequireUser(caller);
        if (caller.IsAdmin)
            return;

        try
        {
            page.EnsureOwnedBy(OwnerId.From(caller.UserId));
        }
        catch (NotPageOwnerException)
        {
            throw new ForbiddenException("You cannot change this page.");
        }
    }

    public static void EnsureCanRead(Page page, IContentCaller caller)
    {
        if (caller.IsInternalService)
            return;

        EnsureCanMutate(page, caller);
    }

    public static void EnsureInternal(IContentCaller caller)
    {
        if (!caller.IsInternalService)
            throw new ForbiddenException("This operation is only available to an internal caller.");
    }

    private static void RequireUser(IContentCaller caller)
    {
        if (caller.UserId == Guid.Empty)
            throw new UnauthorizedException("The caller id is missing.");
    }
}
