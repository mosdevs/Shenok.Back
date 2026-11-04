using Microsoft.AspNetCore.Mvc;
using Shenok.Back.IdentityService.Application.Users.UseCases.GrantRole;

namespace Shenok.Back.IdentityService.Api.Controllers;

[ApiController]
public sealed class InternalUsersController(GrantRoleUseCase grantRole) : ControllerBase
{
    [HttpPost("/internal/users/{userId:guid}/roles")]
    public Task<GrantRoleResponse> GrantRole(Guid userId, GrantRoleBody body, CancellationToken cancellationToken) =>
        grantRole.ExecuteAsync(new GrantRoleRequest(userId, body.Role), cancellationToken);
}

public sealed record GrantRoleBody(string Role);
