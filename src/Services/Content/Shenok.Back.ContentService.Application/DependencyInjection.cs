using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.ContentService.Application.Common.Interfaces;
using Shenok.Back.ContentService.Application.Playback;

namespace Shenok.Back.ContentService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddContentApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPlaylistSigner, HlsPlaylistSigner>();
        services.AddUseCasesFrom(typeof(DependencyInjection).Assembly);
        return services;
    }
}
