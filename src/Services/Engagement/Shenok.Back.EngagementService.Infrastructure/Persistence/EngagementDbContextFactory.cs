using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shenok.Back.EngagementService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c>. The running host uses <c>ConnectionStrings:engagement</c>.
/// </summary>
public sealed class EngagementDbContextFactory : IDesignTimeDbContextFactory<EngagementDbContext>
{
    public EngagementDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=engagement;Username=postgres;Password=postgres")
            .Options;
        return new EngagementDbContext(options);
    }
}
