using FluentValidation;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.IdentityService.Application.Common.Interfaces;
using Shenok.Back.IdentityService.Application.Users.UseCases.GetMyProfile;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.GrantRole;

public sealed record GrantRoleRequest(Guid UserId, string Role);

public sealed record GrantRoleResponse(Guid UserId, IReadOnlyCollection<string> Roles);

public sealed class GrantRoleValidator : AbstractValidator<GrantRoleRequest>
{
    public GrantRoleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role)
            .Must(role => role is nameof(UserRole.ContentCreator) or nameof(UserRole.Admin))
            .WithMessage("Role must be ContentCreator or Admin.");
    }
}

public sealed class GrantRoleUseCase(IUserRepository users, IUnitOfWork unitOfWork)
    : IUseCase<GrantRoleRequest, GrantRoleResponse>
{
    public async Task<GrantRoleResponse> ExecuteAsync(GrantRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(UserId.From(request.UserId), cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var role = Enum.Parse<UserRole>(request.Role);
        user.GrantRole(role);
        await unitOfWork.SaveChanges(cancellationToken);

        var mapped = GetMyProfileUseCase.Map(user);
        return new GrantRoleResponse(mapped.UserId, mapped.Roles);
    }
}
