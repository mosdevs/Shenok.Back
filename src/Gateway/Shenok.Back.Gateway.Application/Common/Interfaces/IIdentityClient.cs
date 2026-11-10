namespace Shenok.Back.Gateway.Application.Common.Interfaces;

public sealed record RegisterBody(
    string? Username,
    string? DisplayName,
    string? Email,
    string? PhoneNumber,
    string? Password,
    string? AvatarUrl,
    string? Bio);

public sealed record OtpRequestBody(string? Email, string? PhoneNumber, string? Purpose);

public sealed record OtpConfirmBody(string? Email, string? PhoneNumber, string? Code, string? Purpose);

public sealed record LoginBody(string? Email, string? PhoneNumber, string? Password);

public sealed record RefreshBody(string? RefreshToken);

public sealed record UpdateProfileBody(string? Username, string? DisplayName, string? AvatarUrl, string? Bio);

public sealed record ChangePasswordBody(string? CurrentPassword, string? NewPassword);

/// <summary>
/// Identity pass-through. The Gateway does not hash passwords, store refresh tokens, or grant roles.
/// Each method returns Identity's status and body. A network failure throws <c>UnavailableException</c>.
/// </summary>
public interface IIdentityClient
{
    Task<ProxiedResponse> RegisterAsync(RegisterBody body, CancellationToken cancellationToken);

    Task<ProxiedResponse> RequestOtpAsync(OtpRequestBody body, CancellationToken cancellationToken);

    Task<ProxiedResponse> ConfirmOtpAsync(OtpConfirmBody body, CancellationToken cancellationToken);

    Task<ProxiedResponse> LoginAsync(LoginBody body, CancellationToken cancellationToken);

    Task<ProxiedResponse> RefreshAsync(RefreshBody body, CancellationToken cancellationToken);

    Task<ProxiedResponse> LogoutAsync(RefreshBody body, CancellationToken cancellationToken);

    Task<ProxiedResponse> GetMeAsync(CancellationToken cancellationToken);

    Task<ProxiedResponse> UpdateMeAsync(UpdateProfileBody body, CancellationToken cancellationToken);

    Task<ProxiedResponse> ChangePasswordAsync(ChangePasswordBody body, CancellationToken cancellationToken);

    Task<ProxiedResponse> GetPublicProfileAsync(string username, CancellationToken cancellationToken);
}
