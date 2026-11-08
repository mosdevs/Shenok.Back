namespace Shenok.Back.Api.Shared.Security;

public static class InternalCaller
{
    public const string ApiKeyHeader = "X-Internal-Key";
    public const string UserIdHeader = "X-User-Id";
    public const string RolesHeader = "X-User-Roles";
    public const string ApiKeyConfigurationPath = "InternalAuth:ApiKey";

    /// <summary>
    /// Request item set only by <see cref="InternalServiceScopeFilter"/>. The value is the Boolean <c>true</c>.
    /// </summary>
    public const string ServiceItemKey = "Shenok.InternalService";
}

public sealed record CallerContext(Guid UserId, IReadOnlyList<string> Roles);
