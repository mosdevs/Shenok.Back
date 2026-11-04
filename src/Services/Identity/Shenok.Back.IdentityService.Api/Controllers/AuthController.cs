using Microsoft.AspNetCore.Mvc;
using Shenok.Back.IdentityService.Application.Auth;
using Shenok.Back.IdentityService.Application.Auth.UseCases.ConfirmOtp;
using Shenok.Back.IdentityService.Application.Auth.UseCases.Login;
using Shenok.Back.IdentityService.Application.Auth.UseCases.Logout;
using Shenok.Back.IdentityService.Application.Auth.UseCases.RefreshSession;
using Shenok.Back.IdentityService.Application.Auth.UseCases.RequestOtp;

namespace Shenok.Back.IdentityService.Api.Controllers;

[ApiController]
[Route("")]
public sealed class AuthController(
    RequestOtpUseCase requestOtp,
    ConfirmOtpUseCase confirmOtp,
    LoginUseCase login,
    RefreshSessionUseCase refreshSession,
    LogoutUseCase logout) : ControllerBase
{
    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp(RequestOtpRequest request, CancellationToken cancellationToken)
    {
        await requestOtp.ExecuteAsync(request, cancellationToken);
        return Ok();
    }

    [HttpPost("otp/confirm")]
    public Task<AuthTokens> ConfirmOtp(ConfirmOtpRequest request, CancellationToken cancellationToken) =>
        confirmOtp.ExecuteAsync(request, cancellationToken);

    [HttpPost("login")]
    public Task<AuthTokens> Login(LoginRequest request, CancellationToken cancellationToken) =>
        login.ExecuteAsync(request, cancellationToken);

    [HttpPost("refresh")]
    public Task<AuthTokens> Refresh(RefreshSessionRequest request, CancellationToken cancellationToken) =>
        refreshSession.ExecuteAsync(request, cancellationToken);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await logout.ExecuteAsync(request, cancellationToken);
        return Ok();
    }
}
