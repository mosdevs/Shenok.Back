namespace Shenok.Back.Gateway.Api.Security;

/// <summary>
/// Claim names Identity writes into the access token.
/// <c>sub</c> stays <c>sub</c> because inbound claim mapping is off.
/// </summary>
internal static class GatewayClaims
{
    public const string Subject = "sub";

    public const string Username = "username";

    public const string Verified = "verified";

    public const string Roles = "roles";

    public const string VerifiedTrue = "true";
}

internal static class GatewayPolicies
{
    public const string VerifiedCaller = "VerifiedCaller";
}
