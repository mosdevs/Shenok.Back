using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Shenok.Back.Api.Shared;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.EngagementService.Application;
using Shenok.Back.EngagementService.Infrastructure;
using Shenok.Back.EngagementService.Infrastructure.Persistence;
using ApplicationDependencyInjection = Shenok.Back.EngagementService.Application.DependencyInjection;

namespace Shenok.Back.EngagementService.Api;

/// <summary>
/// Process gate and HTTP composition for the internal Engagement host.
/// A blank API key stops startup before the process listens. Expected request failures stay HTTP
/// statuses from the shared middleware. This service does not host an actor system.
/// <c>/health</c> is a process check. <c>/ready</c> is the database check. Probes call both without the API key.
/// <c>Database:MigrateOnStartup</c> defaults to true for one local process. Extra replicas set it to false
/// so only one process runs <see cref="EngagementDatabase.MigrateAsync"/>.
/// </summary>
internal static class EngagementHost
{
    public const string ReadyTag = "ready";

    public static void RequireApiKey(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (string.IsNullOrWhiteSpace(configuration[InternalCaller.ApiKeyConfigurationPath]))
            throw new InvalidOperationException("InternalAuth:ApiKey is required.");
    }

    public static void AddServices(IServiceCollection services, IConfiguration configuration, bool includeInfrastructure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        RequireApiKey(configuration);
        services.AddEngagementApplication(configuration);
        if (includeInfrastructure)
            services.AddEngagementInfrastructure(configuration);

        services.AddShenokApi(typeof(ApplicationDependencyInjection).Assembly);
        if (includeInfrastructure)
        {
            services.AddHealthChecks()
                .AddDbContextCheck<EngagementDbContext>(tags: [ReadyTag]);
        }
    }

    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseShenokApi();
        app.MapControllers();
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => !check.Tags.Contains(ReadyTag)
        });
        app.MapHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag)
        });
    }
}
