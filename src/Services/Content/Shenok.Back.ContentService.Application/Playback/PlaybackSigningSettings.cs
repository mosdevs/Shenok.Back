namespace Shenok.Back.ContentService.Application.Playback;

/// <summary>
/// Gateway address and HMAC key used to issue playlist segment URLs.
/// The host registers this before the first playback request. The key is configuration, not a request field.
/// </summary>
public sealed class PlaybackSigningSettings
{
    public const int DefaultLifetimeSeconds = 600;
    public const int MaxLifetimeSeconds = 900;
    public const int MinSigningKeyLength = 32;

    public PlaybackSigningSettings(string gatewayBaseUrl, string signingKey, int lifetimeSeconds = DefaultLifetimeSeconds)
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

        if (lifetimeSeconds <= 0 || lifetimeSeconds > MaxLifetimeSeconds)
        {
            throw new ArgumentException(
                $"Playlist lifetime must be between 1 and {MaxLifetimeSeconds} seconds.",
                nameof(lifetimeSeconds));
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        Authority = uri.GetLeftPart(UriPartial.Authority);
        PathPrefix = path == "/" ? string.Empty : path;
        SigningKey = signingKey;
        Lifetime = TimeSpan.FromSeconds(lifetimeSeconds);
    }

    public string Authority { get; }

    public string PathPrefix { get; }

    public string SigningKey { get; }

    public TimeSpan Lifetime { get; }
}
