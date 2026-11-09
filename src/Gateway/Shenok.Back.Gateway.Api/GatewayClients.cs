namespace Shenok.Back.Gateway.Api;

/// <summary>
/// Named downstream clients. Recognition is registered only when its base URL is set.
/// </summary>
internal static class GatewayClients
{
    public const string Identity = "Identity";

    public const string Content = "Content";

    public const string Engagement = "Engagement";

    public const string Recognition = "Recognition";
}
