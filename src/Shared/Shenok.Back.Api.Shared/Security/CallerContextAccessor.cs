using Microsoft.AspNetCore.Http;

namespace Shenok.Back.Api.Shared.Security;

public interface ICallerContextAccessor
{
    CallerContext GetRequired();
}

public sealed class CallerContextAccessor(IHttpContextAccessor httpContextAccessor) : ICallerContextAccessor
{
    public CallerContext GetRequired()
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HTTP request is in progress.");

        if (!context.Request.Headers.TryGetValue(InternalCaller.UserIdHeader, out var userHeader)
            || !Guid.TryParse(userHeader, out var userId)
            || userId == Guid.Empty)
        {
            throw new Shenok.Back.Application.Abstractions.Exceptions.UnauthorizedException("The caller id is missing.");
        }

        var roles = context.Request.Headers.TryGetValue(InternalCaller.RolesHeader, out var roleHeader)
            ? roleHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        return new CallerContext(userId, roles);
    }
}
