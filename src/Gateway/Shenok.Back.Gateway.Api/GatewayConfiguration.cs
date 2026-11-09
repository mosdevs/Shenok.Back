namespace Shenok.Back.Gateway.Api;

internal static class GatewayConfiguration
{
    public const string JsonTimeoutSecondsKey = "Gateway:JsonTimeoutSeconds";

    public const int DefaultJsonTimeoutSeconds = 10;

    public const string IdentityBaseUrlKey = "Gateway:IdentityBaseUrl";

    public const string ContentBaseUrlKey = "Gateway:ContentBaseUrl";

    public const string EngagementBaseUrlKey = "Gateway:EngagementBaseUrl";

    public const string RecognitionBaseUrlKey = "Gateway:RecognitionBaseUrl";

    public const string AllowFixtureDecoderKey = "Gateway:AllowFixtureDecoder";

    public const string JwtIssuerKey = "Jwt:Issuer";

    public const string JwtAudienceKey = "Jwt:Audience";

    public const string JwtSigningKeyKey = "Jwt:SigningKey";

    public const string DefaultJwtIssuer = "shenok";

    public const string DefaultJwtAudience = "shenok-mobile";

    public const int MinJwtSigningKeyLength = 32;

    public static readonly TimeSpan JwtClockSkew = TimeSpan.FromMinutes(5);

    public const string PlaybackGatewayBaseUrlKey = "Playback:GatewayBaseUrl";

    public const string PlaybackSigningKeyKey = "Playback:SigningKey";
}
