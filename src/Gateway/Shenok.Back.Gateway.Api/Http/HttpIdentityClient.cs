using System.Net.Http.Json;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Http;

internal sealed class HttpIdentityClient(IHttpClientFactory factory) : IIdentityClient
{
    public Task<ProxiedResponse> RegisterAsync(RegisterBody body, CancellationToken cancellationToken) =>
        PostAsync("register", body, cancellationToken);

    public Task<ProxiedResponse> RequestOtpAsync(OtpRequestBody body, CancellationToken cancellationToken) =>
        PostAsync("otp/request", body, cancellationToken);

    public Task<ProxiedResponse> ConfirmOtpAsync(OtpConfirmBody body, CancellationToken cancellationToken) =>
        PostAsync("otp/confirm", body, cancellationToken);

    public Task<ProxiedResponse> LoginAsync(LoginBody body, CancellationToken cancellationToken) =>
        PostAsync("login", body, cancellationToken);

    public Task<ProxiedResponse> RefreshAsync(RefreshBody body, CancellationToken cancellationToken) =>
        PostAsync("refresh", body, cancellationToken);

    public Task<ProxiedResponse> LogoutAsync(RefreshBody body, CancellationToken cancellationToken) =>
        PostAsync("logout", body, cancellationToken);

    public Task<ProxiedResponse> GetMeAsync(CancellationToken cancellationToken) =>
        SendAsync(client => client.GetAsync("users/me", cancellationToken), cancellationToken);

    public Task<ProxiedResponse> UpdateMeAsync(UpdateProfileBody body, CancellationToken cancellationToken) =>
        SendAsync(
            client => client.PatchAsJsonAsync("users/me", body, DownstreamJson.Options, cancellationToken),
            cancellationToken);

    public Task<ProxiedResponse> ChangePasswordAsync(ChangePasswordBody body, CancellationToken cancellationToken) =>
        PostAsync("users/me/password", body, cancellationToken);

    public Task<ProxiedResponse> GetPublicProfileAsync(string username, CancellationToken cancellationToken) =>
        SendAsync(
            client => client.GetAsync($"users/{Uri.EscapeDataString(username)}", cancellationToken),
            cancellationToken);

    private Task<ProxiedResponse> PostAsync(string path, object body, CancellationToken cancellationToken) =>
        SendAsync(
            client => client.PostAsJsonAsync(path, body, DownstreamJson.Options, cancellationToken),
            cancellationToken);

    private Task<ProxiedResponse> SendAsync(
        Func<HttpClient, Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken) =>
        Downstream.ExchangeAsync(() => send(Client()), "Identity", cancellationToken);

    private HttpClient Client() => factory.CreateClient(GatewayClients.Identity);
}
