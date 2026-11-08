using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.RegisterUsers
{
    public sealed record RegisterUserResponse(
        Guid UserId,
        string Email,
        string DisplayName);
}
