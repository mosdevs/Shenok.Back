using Shenok.Back.IdentityService.Domain.Aggregates.Users;

namespace Shenok.Back.IdentityService.Domain.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default);
        Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default);
        Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default);
        Task AddAsync(User user, CancellationToken ct = default);
    }
}
