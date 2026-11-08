using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.RegisterUsers
{
    public sealed record RegisterUserRequest(
        string Email,
        string Password, //Password JUST here is string. never saved and never stored. Hashed immediately.
        string DisplayName);
}
