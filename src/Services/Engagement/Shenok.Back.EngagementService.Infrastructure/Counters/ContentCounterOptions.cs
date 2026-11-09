namespace Shenok.Back.EngagementService.Infrastructure.Counters;

public sealed class ContentCounterOptions
{
    public const string SectionName = "ContentCounters";
    public const string HttpClientName = "content-counters";
    public const string ApiKeyHeader = "X-Internal-Key";
    public const string ApiKeyConfigurationPath = "InternalAuth:ApiKey";

    public string BaseUrl { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 5;
    public int DispatchSeconds { get; set; } = 2;

    public static bool IsValid(ContentCounterOptions options)
    {
        if (options.TimeoutSeconds is < 1 or > 30 || options.DispatchSeconds is < 1 or > 60)
            return false;

        return Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https";
    }
}
