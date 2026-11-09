using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shenok.Back.Gateway.Api.Http;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Tests;

public sealed class GatewayDecoderSelectionTests
{
    [Fact]
    public void Missing_recognition_url_keeps_scans_closed()
    {
        using var provider = Provider(GatewayTestSettings.Create());

        Assert.IsType<ClosedScanDecoder>(provider.GetRequiredService<IScanDecoder>());
    }

    [Fact]
    public void Fixture_decoder_is_selected_only_when_the_flag_is_true()
    {
        var values = GatewayTestSettings.Values();
        values[GatewayConfiguration.AllowFixtureDecoderKey] = "true";
        using var provider = Provider(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        Assert.IsType<FixtureScanDecoder>(provider.GetRequiredService<IScanDecoder>());
    }

    [Fact]
    public void Recognition_url_selects_the_http_decoder()
    {
        var values = GatewayTestSettings.Values();
        values[GatewayConfiguration.RecognitionBaseUrlKey] = "http://127.0.0.1:5300";
        values[GatewayConfiguration.AllowFixtureDecoderKey] = "true";
        using var provider = Provider(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        Assert.IsType<HttpScanDecoder>(provider.GetRequiredService<IScanDecoder>());
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(GatewayClients.Recognition);
        Assert.Equal(new Uri("http://127.0.0.1:5300/"), client.BaseAddress);
    }

    [Fact]
    public void Invalid_recognition_url_is_rejected_before_services_are_registered()
    {
        var services = new ServiceCollection();
        var values = GatewayTestSettings.Values();
        values[GatewayConfiguration.RecognitionBaseUrlKey] = "not-a-url";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GatewayHost.AddServices(services, new ConfigurationBuilder().AddInMemoryCollection(values).Build()));

        Assert.Contains(GatewayConfiguration.RecognitionBaseUrlKey, exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    [Fact]
    public void Fixture_flag_must_be_true_or_false()
    {
        var services = new ServiceCollection();
        var values = GatewayTestSettings.Values();
        values[GatewayConfiguration.AllowFixtureDecoderKey] = "yes";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GatewayHost.AddServices(services, new ConfigurationBuilder().AddInMemoryCollection(values).Build()));

        Assert.Contains(GatewayConfiguration.AllowFixtureDecoderKey, exception.Message, StringComparison.Ordinal);
        Assert.Empty(services);
    }

    private static ServiceProvider Provider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        GatewayHost.AddServices(services, configuration);
        return services.BuildServiceProvider();
    }
}
