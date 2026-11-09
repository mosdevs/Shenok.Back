using Microsoft.Extensions.Configuration;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.Gateway.Api;

namespace Shenok.Back.Gateway.Api.Tests;

internal static class GatewayTestSettings
{
    public const string ApiKey = "gateway-test-key";

    public const string SigningKey = "dev-only-signing-key-32-characters";

    public const string PlaybackKey = "dev-only-playback-signing-key-32b";

    public const string PlaybackUrl = "http://127.0.0.1:5080";

    public const string IdentityBaseUrl = "http://127.0.0.1:5203";

    public const string ContentBaseUrl = "http://127.0.0.1:5117";

    public const string EngagementBaseUrl = "http://127.0.0.1:5217";

    public static Dictionary<string, string?> Values(
        string? apiKey = ApiKey,
        string? signingKey = SigningKey,
        string? issuer = GatewayConfiguration.DefaultJwtIssuer,
        string? audience = GatewayConfiguration.DefaultJwtAudience,
        string? playbackKey = PlaybackKey,
        string? playbackUrl = PlaybackUrl,
        string? identityUrl = IdentityBaseUrl,
        string? contentUrl = ContentBaseUrl,
        string? engagementUrl = EngagementBaseUrl,
        string? timeout = "10",
        bool includeIssuer = true,
        bool includeAudience = true,
        bool includeTimeout = true)
    {
        var values = new Dictionary<string, string?>
        {
            [InternalCaller.ApiKeyConfigurationPath] = apiKey,
            [GatewayConfiguration.JwtSigningKeyKey] = signingKey,
            [GatewayConfiguration.PlaybackSigningKeyKey] = playbackKey,
            [GatewayConfiguration.PlaybackGatewayBaseUrlKey] = playbackUrl,
            [GatewayConfiguration.IdentityBaseUrlKey] = identityUrl,
            [GatewayConfiguration.ContentBaseUrlKey] = contentUrl,
            [GatewayConfiguration.EngagementBaseUrlKey] = engagementUrl
        };

        if (includeIssuer)
            values[GatewayConfiguration.JwtIssuerKey] = issuer;
        if (includeAudience)
            values[GatewayConfiguration.JwtAudienceKey] = audience;
        if (includeTimeout)
            values[GatewayConfiguration.JsonTimeoutSecondsKey] = timeout;

        return values;
    }

    public static IConfiguration Create(
        string? apiKey = ApiKey,
        string? signingKey = SigningKey,
        string? issuer = GatewayConfiguration.DefaultJwtIssuer,
        string? audience = GatewayConfiguration.DefaultJwtAudience,
        string? playbackKey = PlaybackKey,
        string? playbackUrl = PlaybackUrl,
        string? identityUrl = IdentityBaseUrl,
        string? contentUrl = ContentBaseUrl,
        string? engagementUrl = EngagementBaseUrl,
        string? timeout = "10",
        bool includeIssuer = true,
        bool includeAudience = true,
        bool includeTimeout = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(Values(
            apiKey,
            signingKey,
            issuer,
            audience,
            playbackKey,
            playbackUrl,
            identityUrl,
            contentUrl,
            engagementUrl,
            timeout,
            includeIssuer,
            includeAudience,
            includeTimeout)).Build();
}
