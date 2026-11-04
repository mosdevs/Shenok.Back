using Microsoft.AspNetCore.Mvc;
using Shenok.Back.Api.Shared.Security;
using Shenok.Back.IdentityService.Application.Users.UseCases.ChangePassword;
using Shenok.Back.IdentityService.Application.Users.UseCases.GetMyProfile;
using Shenok.Back.IdentityService.Application.Users.UseCases.GetPublicProfile;
using Shenok.Back.IdentityService.Application.Users.UseCases.RegisterUser;
using Shenok.Back.IdentityService.Application.Users.UseCases.UpdateProfile;

namespace Shenok.Back.IdentityService.Api.Controllers;

[ApiController]
public sealed class UsersController(
    RegisterUserUseCase registerUser,
    GetMyProfileUseCase getMyProfile,
    UpdateProfileUseCase updateProfile,
    ChangePasswordUseCase changePassword,
    GetPublicProfileUseCase getPublicProfile,
    ICallerContextAccessor caller) : ControllerBase
{
    [HttpPost("/register")]
    public Task<RegisterUserResponse> Register(RegisterUserRequest request, CancellationToken cancellationToken) =>
        registerUser.ExecuteAsync(request, cancellationToken);

    [HttpGet("/users/me")]
    public Task<UserProfileResponse> Me(CancellationToken cancellationToken) =>
        getMyProfile.ExecuteAsync(new GetMyProfileRequest(caller.GetRequired().UserId), cancellationToken);

    [HttpPatch("/users/me")]
    public Task<UserProfileResponse> Update(UpdateProfileBody body, CancellationToken cancellationToken)
    {
        var userId = caller.GetRequired().UserId;
        return updateProfile.ExecuteAsync(
            new UpdateProfileRequest(userId, body.Username, body.DisplayName, body.AvatarUrl, body.Bio),
            cancellationToken);
    }

    [HttpPost("/users/me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordBody body, CancellationToken cancellationToken)
    {
        await changePassword.ExecuteAsync(
            new ChangePasswordRequest(caller.GetRequired().UserId, body.CurrentPassword, body.NewPassword),
            cancellationToken);
        return Ok();
    }

    [HttpGet("/users/{username}")]
    public Task<PublicProfileResponse> PublicProfile(string username, CancellationToken cancellationToken) =>
        getPublicProfile.ExecuteAsync(new GetPublicProfileRequest(username), cancellationToken);
}
