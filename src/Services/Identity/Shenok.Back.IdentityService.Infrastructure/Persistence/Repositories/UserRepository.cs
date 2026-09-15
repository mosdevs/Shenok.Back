using Microsoft.EntityFrameworkCore;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;
using System.Threading.Tasks;

namespace Shenok.Back.IdentityService.Infrastructure.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IdentityDbContext _dbContext;

        public UserRepository(IdentityDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(User user, CancellationToken ct = default)
        {
            await _dbContext.Users.AddAsync(user, ct);
        }

        public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default)
        {
            return await _dbContext.Users
                .AnyAsync(u => u.Email == email, ct);
        }

        public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default)
        {
            return await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == email, ct);
        }

        public async Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default)
        {
            return await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == id, ct);
        }

    }
}
