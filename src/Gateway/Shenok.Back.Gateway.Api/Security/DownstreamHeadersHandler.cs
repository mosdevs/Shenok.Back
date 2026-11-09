using System.Net.Http.Headers;
using System.Security.Claims;
using Shenok.Back.Api.Shared.Security;

namespace Shenok.Back.Gateway.Api.Security;

internal sealed class DownstreamInternalKey
{
    public DownstreamInternalKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("InternalAuth:ApiKey is required.");

        Value = value;
    }

    public string Value { get; }
}

/// <summary>
/// Adds the internal credential and the caller taken from the access token.
/// A request body is never the source of the user id. No caller means the user headers are omitted.
/// A role value that itself contains a comma is dropped so the downstream split cannot invent a role.
/// </summary>
internal sealed class DownstreamHeadersHandler(
    IHttpContextAccessor httpContextAccessor,
    DownstreamInternalKey internalKey) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Set(request.Headers, InternalCaller.ApiKeyHeader, internalKey.Value);

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
            CopyCaller(request.Headers, user);

        return base.SendAsync(request, cancellationToken);
    }

    private static void CopyCaller(HttpRequestHeaders headers, ClaimsPrincipal user)
    {
        var subject = user.FindFirst(GatewayClaims.Subject)?.Value;
        if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty)
            return;

        Set(headers, InternalCaller.UserIdHeader, userId.ToString("D"));

        var roles = user.FindAll(GatewayClaims.Roles)
            .Select(claim => claim.Value.Trim())
            .Where(role => role.Length > 0 && !role.Contains(','))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (roles.Length == 0)
            return;

        Set(headers, InternalCaller.RolesHeader, string.Join(',', roles));
    }

    private static void Set(HttpRequestHeaders headers, string name, string value)
    {
        headers.Remove(name);
        headers.TryAddWithoutValidation(name, value);
    }
}
