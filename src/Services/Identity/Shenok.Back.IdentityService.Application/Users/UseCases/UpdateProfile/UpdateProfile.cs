using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Application.Users.UseCases.GetMyProfile;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.UpdateProfile;

public sealed record UpdateProfileBody(string Username, string DisplayName, string? AvatarUrl, string? Bio);

public sealed record UpdateProfileRequest(Guid UserId, string Username, string DisplayName, string? AvatarUrl, string? Bio);

public sealed class UpdateProfileBodyValidator : AbstractValidator<UpdateProfileBody>
{
    public UpdateProfileBodyValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Bio).MaximumLength(500);
    }
}

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Username).NotEmpty().MaximumLength(30);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Bio).MaximumLength(500);
    }
}

public sealed class UpdateProfileUseCase(IUserRepository users, IUnitOfWork unitOfWork)
    : IUseCase<UpdateProfileRequest, UserProfileResponse>
{
    public async Task<UserProfileResponse> ExecuteAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(UserId.From(request.UserId), cancellationToken)
            ?? throw new NotFoundException("User was not found.");
        var profile = UserProfile.Create(request.Username, request.DisplayName, request.AvatarUrl, request.Bio);
        if (!string.Equals(profile.Username, user.Profile.Username, StringComparison.Ordinal)
            && await users.ExistsByUsernameAsync(profile.Username, cancellationToken))
        {
            throw new ConflictException("Username is already registered.");
        }

        user.UpdateProfile(profile);
        await unitOfWork.SaveChanges(cancellationToken);
        return GetMyProfileUseCase.Map(user);
    }
}
