using Microsoft.EntityFrameworkCore;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence;

/// <summary>
/// Startup migration switch shared with the API host.
/// Missing <see cref="MigrateOnStartupKey"/> means migrate. False skips <see cref="MigrateAsync"/>.
/// </summary>
public static class EngagementDatabase
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    public static bool ShouldMigrate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetValue(MigrateOnStartupKey, true);
    }

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        var configuration = services.GetRequiredService<IConfiguration>();
        if (!ShouldMigrate(configuration))
            return;

        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<EngagementDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }
}
