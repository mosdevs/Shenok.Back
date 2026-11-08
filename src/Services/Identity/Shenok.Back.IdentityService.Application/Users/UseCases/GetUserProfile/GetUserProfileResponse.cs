using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace Shenok.Back.IdentityService.Application.Users.UseCases.GetUserProfile
{
    public sealed record GetUserProfileResponse(
        Guid Id,
        string DisplayName,
        string? AvatarUrl,
        string? Bio);
}
