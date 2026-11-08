using Microsoft.Extensions.Configuration;
using Shenok.Back.Api.Shared.Security;

namespace Shenok.Back.ContentService.Api;

internal static class ContentHost
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";

    public static void RequireApiKey(IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration[InternalCaller.ApiKeyConfigurationPath]))
            throw new InvalidOperationException("InternalAuth:ApiKey is required.");
    }

    public static bool ShouldMigrate(IConfiguration configuration) =>
        configuration.GetValue(MigrateOnStartupKey, true);
}
