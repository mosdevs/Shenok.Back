using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.GetPublicProfile;

public sealed record GetPublicProfileRequest(string Username);

public sealed record PublicProfileResponse(string Username, string DisplayName, string? AvatarUrl, string? Bio);

public sealed class GetPublicProfileValidator : AbstractValidator<GetPublicProfileRequest>
{
    public GetPublicProfileValidator() => RuleFor(x => x.Username).NotEmpty();
}

public sealed class GetPublicProfileUseCase(IUserRepository users) : IUseCase<GetPublicProfileRequest, PublicProfileResponse>
{
    public async Task<PublicProfileResponse> ExecuteAsync(GetPublicProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByUsernameAsync(request.Username.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new NotFoundException("User was not found.");
        return new PublicProfileResponse(user.Profile.Username, user.Profile.DisplayName, user.Profile.AvatarUrl, user.Profile.Bio);
    }
}
