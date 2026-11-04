using Microsoft.EntityFrameworkCore;
using Shenok.Back.IdentityService.Domain.Aggregates.Otps;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Infrastructure.Persistence.Repositories;

public sealed class OtpChallengeRepository(IdentityDbContext db) : IOtpChallengeRepository
{
    public Task AddAsync(OtpChallenge challenge, CancellationToken ct = default) =>
        db.OtpChallenges.AddAsync(challenge, ct).AsTask();

    public Task<OtpChallenge?> GetActiveAsync(
        UserId userId,
        OtpPurpose purpose,
        string destination,
        DateTime utcNow,
        CancellationToken ct = default) =>
        db.OtpChallenges.AsTracking()
            .Where(challenge => challenge.UserId == userId
                && challenge.Purpose == purpose
                && challenge.Destination == destination
                && !challenge.IsConsumed
                && challenge.ExpiresAt > utcNow
                && challenge.AttemptCount < OtpChallenge.MaxAttempts)
            .OrderByDescending(challenge => challenge.ExpiresAt)
            .FirstOrDefaultAsync(ct);
}
