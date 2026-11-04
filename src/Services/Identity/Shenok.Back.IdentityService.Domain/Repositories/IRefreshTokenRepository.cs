using Shenok.Back.IdentityService.Domain.Aggregates.RefreshTokens;

namespace Shenok.Back.IdentityService.Domain.Repositories;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
}
