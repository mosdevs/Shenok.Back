namespace Shenok.Back.Gateway.Api.Playback;

/// <summary>
/// Public origin and HMAC key for playlist URLs.
/// The URL and key rules match Content's <c>PlaybackSigningSettings</c>.
/// This service does not reference Content, and it does not choose the playlist lifetime.
/// Content writes <c>exp</c>. The Gateway checks that value when the playlist and segment routes exist.
/// </summary>
internal sealed class GatewayPlaybackSettings
{
    public const int MinSigningKeyLength = 32;

    public GatewayPlaybackSettings(string gatewayBaseUrl, string signingKey)
    {
        if (string.IsNullOrWhiteSpace(gatewayBaseUrl))
            throw new ArgumentException("Gateway base URL is required.", nameof(gatewayBaseUrl));

        var candidate = gatewayBaseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException(
                "Gateway base URL must be an absolute http(s) URL without credentials, a query, or a fragment.",
                nameof(gatewayBaseUrl));
        }

        if (uri.Scheme != "https" && !uri.IsLoopback)
            throw new ArgumentException("Gateway base URL must use https except on loopback.", nameof(gatewayBaseUrl));

        if (string.IsNullOrEmpty(signingKey)
            || signingKey.Length < MinSigningKeyLength
            || signingKey.Trim().Length != signingKey.Length)
        {
            throw new ArgumentException(
                $"Signing key must be at least {MinSigningKeyLength} characters and have no surrounding whitespace.",
                nameof(signingKey));
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        Authority = uri.GetLeftPart(UriPartial.Authority);
        PathPrefix = path == "/" ? string.Empty : path;
        SigningKey = signingKey;
    }

    public string Authority { get; }

    public string PathPrefix { get; }

    public string SigningKey { get; }
}
