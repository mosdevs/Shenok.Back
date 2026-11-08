using Shenok.Back.Api.Shared.Security;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common.Interfaces;

namespace Shenok.Back.ContentService.Api.Security;

/// <summary>
/// Reads the caller from Gateway headers. The request body is never the source of the user id or roles.
/// <see cref="IsInternalService"/> is true only when the selected endpoint is marked <see cref="InternalServiceAttribute"/>.
/// A path prefix and a client header cannot set it.
/// </summary>
public sealed class HttpContentCaller(IHttpContextAccessor httpContextAccessor) : IContentCaller
{
    public Guid UserId
    {
        get
        {
            var http = RequireHttp();
            if (!http.Request.Headers.TryGetValue(InternalCaller.UserIdHeader, out var userHeader)
                || !Guid.TryParse(userHeader, out var userId)
                || userId == Guid.Empty)
            {
                throw new UnauthorizedException("The caller id is missing.");
            }

            return userId;
        }
    }

    public bool IsAdmin => HasRole("Admin");

    public bool IsContentCreator => HasRole("ContentCreator");

    public bool IsInternalService
    {
        get
        {
            var http = RequireHttp();
            return http.Items.TryGetValue(InternalCaller.ServiceItemKey, out var marked) && marked is true;
        }
    }

    private bool HasRole(string role)
    {
        var http = RequireHttp();
        if (!http.Request.Headers.TryGetValue(InternalCaller.RolesHeader, out var roleHeader))
            return false;

        var roles = roleHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return roles.Contains(role, StringComparer.Ordinal);
    }

    private HttpContext RequireHttp() =>
        httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("No HTTP request is in progress.");
}
