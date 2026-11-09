using Microsoft.EntityFrameworkCore;

namespace Shenok.Back.RecognitionService.Infrastructure.Persistence;

/// <summary>
/// Startup migration switch shared with the API host.
/// Missing <see cref="MigrateOnStartupKey"/> means migrate. False skips <see cref="MigrateAsync"/>.
/// Ready probes do not migrate.
/// </summary>
public static class RecognitionDatabase
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
        var database = scope.ServiceProvider.GetRequiredService<RecognitionDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
    }
}
