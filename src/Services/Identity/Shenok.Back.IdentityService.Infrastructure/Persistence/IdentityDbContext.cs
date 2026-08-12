using Microsoft.EntityFrameworkCore;
using Shenok.Back.IdentityService.Domain.Aggregates.Users;
using Shenok.Back.IdentityService.Infrastructure.Configurations;

namespace Shenok.Back.IdentityService.Infrastructure.Persistence
{
    public class IdentityDbContext : DbContext
    {
        //how do I talk to the database
        public DbSet<User> Users => Set<User>();
        public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) //?
        {
            
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            base.OnModelCreating(modelBuilder);
        }
    }
}
