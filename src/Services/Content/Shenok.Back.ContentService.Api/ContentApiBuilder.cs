using Shenok.Back.ContentService.Api.Security;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Application.Playback;

namespace Shenok.Back.ContentService.Api;

internal static class ContentApiBuilder
{
    public static IServiceCollection AddContentApi(this IServiceCollection services, IConfiguration configuration)
    {
        var playback = configuration.GetSection("Playback");
        var lifetime = playback.GetValue("LifetimeSeconds", PlaybackSigningSettings.DefaultLifetimeSeconds);
        services.AddSingleton(new PlaybackSigningSettings(
            playback["GatewayBaseUrl"] ?? string.Empty,
            playback["SigningKey"] ?? string.Empty,
            lifetime));
        services.AddScoped<IContentCaller, HttpContentCaller>();
        return services;
    }
}
