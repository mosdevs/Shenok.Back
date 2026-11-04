using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.GetMyProfile;

public sealed record GetMyProfileRequest(Guid UserId);

public sealed record UserProfileResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    string? Email,
    string? PhoneNumber,
    IReadOnlyCollection<string> Roles,
    string VerificationStatus);

public sealed class GetMyProfileUseCase(IUserRepository users) : IUseCase<GetMyProfileRequest, UserProfileResponse>
{
    public async Task<UserProfileResponse> ExecuteAsync(GetMyProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(UserId.From(request.UserId), cancellationToken)
            ?? throw new NotFoundException("User was not found.");
        return Map(user);
    }

    internal static UserProfileResponse Map(User user) => new(
        user.Id.Value,
        user.Profile.Username,
        user.Profile.DisplayName,
        user.Profile.AvatarUrl,
        user.Profile.Bio,
        user.Email?.Value,
        user.PhoneNumber?.Value,
        user.Roles.Select(role => role.ToString()).ToArray(),
        user.VerificationStatus.ToString());
}
