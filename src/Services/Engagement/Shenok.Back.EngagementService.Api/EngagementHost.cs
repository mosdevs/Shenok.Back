using Shenok.Back.Api.Shared.Security;

namespace Shenok.Back.EngagementService.Api;

/// <summary>
/// Process gate for the internal Engagement host.
/// A blank API key stops startup before the process listens. Expected request failures stay HTTP
/// statuses from the shared middleware: they are not actor restarts, and this service does not
/// host an actor system.
/// </summary>
internal static class EngagementHost
{
    public static void RequireApiKey(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (string.IsNullOrWhiteSpace(configuration[InternalCaller.ApiKeyConfigurationPath]))
            throw new InvalidOperationException("InternalAuth:ApiKey is required.");
    }
}
