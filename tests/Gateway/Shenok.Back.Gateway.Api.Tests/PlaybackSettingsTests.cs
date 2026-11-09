using Shenok.Back.Gateway.Api.Playback;

namespace Shenok.Back.Gateway.Api.Tests;

public sealed class PlaybackSettingsTests
{
    private static readonly string Key = new('k', GatewayPlaybackSettings.MinSigningKeyLength);

    [Fact]
    public void Loopback_http_origin_has_no_path_prefix()
    {
        var settings = new GatewayPlaybackSettings("http://127.0.0.1:5080/", Key);

        Assert.Equal("http://127.0.0.1:5080", settings.Authority);
        Assert.Equal(string.Empty, settings.PathPrefix);
        Assert.Equal(Key, settings.SigningKey);
    }

    [Fact]
    public void Https_origin_keeps_the_path_prefix()
    {
        var settings = new GatewayPlaybackSettings("https://api.example.com/edge/", Key);

        Assert.Equal("https://api.example.com", settings.Authority);
        Assert.Equal("/edge", settings.PathPrefix);
    }

    [Fact]
    public void Localhost_http_is_allowed()
    {
        var settings = new GatewayPlaybackSettings("http://localhost:5080", Key);

        Assert.Equal("http://localhost:5080", settings.Authority);
        Assert.Equal(string.Empty, settings.PathPrefix);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_base_url_is_rejected(string url)
    {
        var exception = Assert.Throws<ArgumentException>(() => new GatewayPlaybackSettings(url, Key));

        Assert.StartsWith("Gateway base URL is required.", exception.Message, StringComparison.Ordinal);
        Assert.Equal("gatewayBaseUrl", exception.ParamName);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("/relative")]
    [InlineData("http://user:pass@127.0.0.1:5080")]
    [InlineData("http://127.0.0.1:5080?x=1")]
    [InlineData("http://127.0.0.1:5080#part")]
    public void Malformed_base_url_is_rejected(string url)
    {
        var exception = Assert.Throws<ArgumentException>(() => new GatewayPlaybackSettings(url, Key));

        Assert.StartsWith(
            "Gateway base URL must be an absolute http(s) URL without credentials, a query, or a fragment.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Public_http_origin_is_rejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => new GatewayPlaybackSettings("http://example.com", Key));

        Assert.StartsWith("Gateway base URL must use https except on loopback.", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(0)]
    public void Short_signing_key_is_rejected(int length)
    {
        var exception = Assert.Throws<ArgumentException>(() => new GatewayPlaybackSettings("https://example.com", new string('k', length)));

        Assert.StartsWith(
            "Signing key must be at least 32 characters and have no surrounding whitespace.",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal("signingKey", exception.ParamName);
    }

    [Fact]
    public void Signing_key_with_surrounding_whitespace_is_rejected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new GatewayPlaybackSettings("https://example.com", $" {Key}"));

        Assert.StartsWith(
            "Signing key must be at least 32 characters and have no surrounding whitespace.",
            exception.Message,
            StringComparison.Ordinal);
    }
}
