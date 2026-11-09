using Shenok.Back.Api.Shared.Errors;

namespace Shenok.Back.Gateway.Api.Security;

internal static class GatewayAuthResponses
{
    public const string MissingOrInvalidToken = "The access token is missing or invalid.";

    public const string NotVerified = "The caller is not verified.";

    public const string NotAllowed = "The caller is not allowed to use this route.";

    public static Task WriteUnauthorizedAsync(HttpContext http) =>
        ApiProblems.WriteAsync(http, new ApiProblem(
            "https://shenok.dev/problems/unauthorized",
            "Unauthorized",
            StatusCodes.Status401Unauthorized,
            MissingOrInvalidToken,
            http.TraceIdentifier,
            null));

    public static Task WriteForbiddenAsync(HttpContext http, string detail) =>
        ApiProblems.WriteAsync(http, new ApiProblem(
            "https://shenok.dev/problems/forbidden",
            "Forbidden",
            StatusCodes.Status403Forbidden,
            detail,
            http.TraceIdentifier,
            null));
}
