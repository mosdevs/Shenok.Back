using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Gateway.Api.Http;
using Shenok.Back.Gateway.Application.Common.Interfaces;

namespace Shenok.Back.Gateway.Api.Controllers;

[ApiController]
public sealed class IdentityController(IIdentityClient identity) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("/register")]
    public Task<IActionResult> Register(RegisterBody? body, CancellationToken cancellationToken) =>
        Proxy(identity.RegisterAsync(body ?? EmptyRegister(), cancellationToken));

    [AllowAnonymous]
    [HttpPost("/otp/request")]
    public Task<IActionResult> RequestOtp(OtpRequestBody? body, CancellationToken cancellationToken) =>
        Proxy(identity.RequestOtpAsync(body ?? new OtpRequestBody(null, null, null), cancellationToken));

    [AllowAnonymous]
    [HttpPost("/otp/confirm")]
    public Task<IActionResult> ConfirmOtp(OtpConfirmBody? body, CancellationToken cancellationToken) =>
        Proxy(identity.ConfirmOtpAsync(body ?? new OtpConfirmBody(null, null, null, null), cancellationToken));

    [AllowAnonymous]
    [HttpPost("/login")]
    public Task<IActionResult> Login(LoginBody? body, CancellationToken cancellationToken) =>
        Proxy(identity.LoginAsync(body ?? new LoginBody(null, null, null), cancellationToken));

    [HttpPost("/refresh")]
    public Task<IActionResult> Refresh(RefreshBody? body, CancellationToken cancellationToken) =>
        Proxy(identity.RefreshAsync(body ?? new RefreshBody(null), cancellationToken));

    [HttpPost("/logout")]
    public Task<IActionResult> Logout(RefreshBody? body, CancellationToken cancellationToken) =>
        Proxy(identity.LogoutAsync(body ?? new RefreshBody(null), cancellationToken));

    [HttpGet("/users/me")]
    public Task<IActionResult> Me(CancellationToken cancellationToken) =>
        Proxy(identity.GetMeAsync(cancellationToken));

    [HttpPatch("/users/me")]
    public Task<IActionResult> Update(UpdateProfileBody? body, CancellationToken cancellationToken) =>
        Proxy(identity.UpdateMeAsync(body ?? new UpdateProfileBody(null, null, null, null), cancellationToken));

    [HttpPost("/users/me/password")]
    public Task<IActionResult> ChangePassword(ChangePasswordBody? body, CancellationToken cancellationToken) =>
        Proxy(identity.ChangePasswordAsync(body ?? new ChangePasswordBody(null, null), cancellationToken));

    [HttpGet("/users/{username}")]
    public Task<IActionResult> PublicProfile(string username, CancellationToken cancellationToken) =>
        Proxy(identity.GetPublicProfileAsync(username, cancellationToken));

    private static async Task<IActionResult> Proxy(Task<ProxiedResponse> call) =>
        ProxiedResults.From(await call);

    private static RegisterBody EmptyRegister() =>
        new(null, null, null, null, null, null, null);
}
