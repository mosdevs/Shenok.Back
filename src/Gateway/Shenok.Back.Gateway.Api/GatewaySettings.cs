using Shenok.Back.Api.Shared.Security;
using Shenok.Back.Gateway.Api.Playback;

namespace Shenok.Back.Gateway.Api;

internal sealed record GatewaySettings(
    string InternalApiKey,
    string JwtIssuer,
    string JwtAudience,
    string JwtSigningKey,
    GatewayPlaybackSettings Playback,
    Uri IdentityBaseUrl,
    Uri ContentBaseUrl,
    Uri EngagementBaseUrl,
    Uri? RecognitionBaseUrl,
    bool AllowFixtureDecoder,
    TimeSpan JsonTimeout)
{
    public static GatewaySettings Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var apiKey = configuration[InternalCaller.ApiKeyConfigurationPath];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("InternalAuth:ApiKey is required.");

        var signingKey = configuration[GatewayConfiguration.JwtSigningKeyKey];
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < GatewayConfiguration.MinJwtSigningKeyLength)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

        var issuer = configuration[GatewayConfiguration.JwtIssuerKey];
        var audience = configuration[GatewayConfiguration.JwtAudienceKey];

        return new GatewaySettings(
            apiKey,
            string.IsNullOrWhiteSpace(issuer) ? GatewayConfiguration.DefaultJwtIssuer : issuer,
            string.IsNullOrWhiteSpace(audience) ? GatewayConfiguration.DefaultJwtAudience : audience,
            signingKey,
            new GatewayPlaybackSettings(
                configuration[GatewayConfiguration.PlaybackGatewayBaseUrlKey] ?? string.Empty,
                configuration[GatewayConfiguration.PlaybackSigningKeyKey] ?? string.Empty),
            RequireBaseUrl(configuration, GatewayConfiguration.IdentityBaseUrlKey),
            RequireBaseUrl(configuration, GatewayConfiguration.ContentBaseUrlKey),
            RequireBaseUrl(configuration, GatewayConfiguration.EngagementBaseUrlKey),
            OptionalBaseUrl(configuration, GatewayConfiguration.RecognitionBaseUrlKey),
            ReadFlag(configuration, GatewayConfiguration.AllowFixtureDecoderKey),
            RequireTimeout(configuration));
    }

    private static Uri RequireBaseUrl(IConfiguration configuration, string key)
    {
        if (NormalizeBaseUrl(configuration[key]) is not Uri uri)
        {
            throw new InvalidOperationException(
                $"{key} must be an absolute http(s) URL without credentials, a query, or a fragment.");
        }

        return uri;
    }

    private static Uri? OptionalBaseUrl(IConfiguration configuration, string key)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (NormalizeBaseUrl(raw) is not Uri uri)
        {
            throw new InvalidOperationException(
                $"{key} must be an absolute http(s) URL without credentials, a query, or a fragment.");
        }

        return uri;
    }

    private static bool ReadFlag(IConfiguration configuration, string key)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (bool.TryParse(raw, out var value))
            return value;

        throw new InvalidOperationException($"{key} must be true or false.");
    }

    private static Uri? NormalizeBaseUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var parsed)
            || parsed.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(parsed.UserInfo)
            || !string.IsNullOrEmpty(parsed.Query)
            || !string.IsNullOrEmpty(parsed.Fragment))
        {
            return null;
        }

        var normalized = parsed.GetLeftPart(UriPartial.Path);
        if (!normalized.EndsWith('/'))
            normalized += "/";

        return new Uri(normalized, UriKind.Absolute);
    }

    private static TimeSpan RequireTimeout(IConfiguration configuration)
    {
        int seconds;
        try
        {
            seconds = configuration.GetValue(
                GatewayConfiguration.JsonTimeoutSecondsKey,
                GatewayConfiguration.DefaultJsonTimeoutSeconds);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                "Gateway:JsonTimeoutSeconds must be a positive number of seconds.",
                exception);
        }

        if (seconds <= 0)
            throw new InvalidOperationException("Gateway:JsonTimeoutSeconds must be greater than zero.");

        return TimeSpan.FromSeconds(seconds);
    }
}
