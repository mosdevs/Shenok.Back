using Shenok.Back.IdentityService.Domain.Aggregates.Users;

namespace Shenok.Back.IdentityService.Application.Common.Interfaces;

public interface ITokenService
{
    (string AccessToken, DateTime ExpiresAt) CreateAccessToken(User user);
    string CreateRefreshTokenValue();
    string Hash(string value);
    TimeSpan RefreshTokenLifetime { get; }
}
