using Shenok.Back.Api.Shared.Correlation;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.Gateway.Api;
using Shenok.Back.Gateway.Api.Security;

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
