using Shenok.Back.Api.Shared.Correlation;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.Gateway.Api.Security;
using GatewayClients = Shenok.Back.Gateway.Api.GatewayClients;
using GatewaySettings = Shenok.Back.Gateway.Api.GatewaySettings;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Http;

internal static class DownstreamClients
{
    private static readonly string[] RedactedHeaders =
    [
        InternalCaller.ApiKeyHeader,
        "Authorization",
        "Cookie",
        "Set-Cookie"
    ];

    public static void Add(IServiceCollection services, GatewaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        services.AddTransient<CorrelationIdDelegatingHandler>();
        services.AddTransient<DownstreamHeadersHandler>();
        services.AddSingleton(new DownstreamInternalKey(settings.InternalApiKey));

        AddClient(services, GatewayClients.Identity, settings.IdentityBaseUrl, settings.JsonTimeout);
        AddClient(services, GatewayClients.Content, settings.ContentBaseUrl, settings.JsonTimeout);
        AddClient(services, GatewayClients.Engagement, settings.EngagementBaseUrl, settings.JsonTimeout);
        services.AddSingleton<IContentGateway, HttpContentGateway>();
        services.AddSingleton<IEngagementGateway, HttpEngagementGateway>();
        services.AddSingleton<IIdentityClient, HttpIdentityClient>();
        services.AddSingleton<IPlaybackLink, PlaybackLink>();

        if (settings.RecognitionBaseUrl is Uri recognition)
        {
            AddClient(services, GatewayClients.Recognition, recognition, settings.JsonTimeout);
            services.AddSingleton<IScanDecoder, HttpScanDecoder>();
        }
        else if (settings.AllowFixtureDecoder)
        {
            services.AddSingleton<IScanDecoder, FixtureScanDecoder>();
        }
        else
        {
            services.AddSingleton<IScanDecoder, ClosedScanDecoder>();
        }
    }

    private static void AddClient(IServiceCollection services, string name, Uri baseAddress, TimeSpan timeout)
    {
        services.AddHttpClient(name, client =>
            {
                client.BaseAddress = baseAddress;
                client.Timeout = timeout;
            })
            .AddHttpMessageHandler<DownstreamHeadersHandler>()
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>()
            .RedactLoggedHeaders(RedactedHeaders);
    }
}
