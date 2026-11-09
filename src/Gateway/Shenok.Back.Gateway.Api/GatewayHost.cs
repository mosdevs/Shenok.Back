using Microsoft.AspNetCore.Authorization;
using Shenok.Back.Api.Shared;
using Shenok.Back.Gateway.Api.Http;
using Shenok.Back.Gateway.Api.Security;
using Shenok.Back.Gateway.Application;
using ApplicationDependencyInjection = Shenok.Back.Gateway.Application.DependencyInjection;

namespace Shenok.Back.Gateway.Api;

/// <summary>
/// Process gate and HTTP composition for the public Gateway.
/// A blank internal key, a short JWT signing key, or an invalid playback URL stops startup before the process listens.
/// Inbound requests do not accept <c>X-Internal-Key</c>. The Gateway adds that header on the way out.
/// <c>/health</c> is a process check. It does not call Identity, Content, or Engagement.
/// This service has no database and no actor system.
/// </summary>
internal static class GatewayHost
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = GatewaySettings.Read(configuration);

        services.AddGatewayApplication();
        services.AddShenokApi(typeof(ApplicationDependencyInjection).Assembly, requireInternalApiKey: false);
        services.AddSingleton(settings.Playback);
        GatewayAuthentication.Add(services, settings);
        // After AddAuthorization so this handler is the one the middleware resolves.
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemAuthorizationResultHandler>();
        DownstreamClients.Add(services, settings);
    }

    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseShenokApi();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health").AllowAnonymous();
    }
}
