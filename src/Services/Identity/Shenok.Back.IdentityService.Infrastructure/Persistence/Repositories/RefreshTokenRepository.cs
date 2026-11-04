using Microsoft.EntityFrameworkCore;
using Shenok.Back.IdentityService.Domain.Aggregates.RefreshTokens;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(IdentityDbContext db) : IRefreshTokenRepository
{
    public Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        db.RefreshTokens.AddAsync(token, ct).AsTask();

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens.AsTracking().FirstOrDefaultAsync(token => token.TokenHash == tokenHash, ct);
}
