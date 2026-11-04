using Shenok.Back.IdentityService.Domain.Aggregates.Otps;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;

namespace Shenok.Back.IdentityService.Domain.Repositories;

public interface IOtpChallengeRepository
{
    Task AddAsync(OtpChallenge challenge, CancellationToken ct = default);
    Task<OtpChallenge?> GetActiveAsync(
        UserId userId,
        OtpPurpose purpose,
        string destination,
        DateTime utcNow,
        CancellationToken ct = default);
}
