namespace Shenok.Back.Gateway.Api;

/// <summary>
/// Named <see cref="System.Net.Http.HttpClient"/> registrations for the services this process calls.
/// Recognition is not one of them. Its client arrives with the decode port.
/// </summary>
internal static class GatewayClients
{
    public const string Identity = "Identity";

    public const string Content = "Content";

    public const string Engagement = "Engagement";
}
