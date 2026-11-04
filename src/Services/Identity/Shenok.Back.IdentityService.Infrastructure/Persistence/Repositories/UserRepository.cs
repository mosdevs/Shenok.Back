using Microsoft.EntityFrameworkCore;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Domain.Repositories;

namespace Shenok.Back.IdentityService.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext db) : IUserRepository
{
    public Task AddAsync(User user, CancellationToken ct = default) =>
        db.Users.AddAsync(user, ct).AsTask();

    public Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default) =>
        db.Users.AnyAsync(user => user.Email != null && user.Email.Value == email.Value, ct);

    public Task<bool> ExistsByPhoneAsync(PhoneNumber phoneNumber, CancellationToken ct = default) =>
        db.Users.AnyAsync(user => user.PhoneNumber != null && user.PhoneNumber.Value == phoneNumber.Value, ct);

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default) =>
        db.Users.AnyAsync(user => user.Profile.Username == username, ct);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default) =>
        db.Users.AsTracking().FirstOrDefaultAsync(user => user.Email != null && user.Email.Value == email.Value, ct);

    public Task<User?> GetByIdAsync(UserId id, CancellationToken ct = default) =>
        db.Users.AsTracking().FirstOrDefaultAsync(user => user.Id == id, ct);

    public Task<User?> GetByPhoneAsync(PhoneNumber phoneNumber, CancellationToken ct = default) =>
        db.Users.AsTracking().FirstOrDefaultAsync(user => user.PhoneNumber != null && user.PhoneNumber.Value == phoneNumber.Value, ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        db.Users.AsTracking().FirstOrDefaultAsync(user => user.Profile.Username == username, ct);
}
