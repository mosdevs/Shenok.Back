using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shenok.Back.ContentService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c>. The running host uses <c>ConnectionStrings:content</c>.
/// </summary>
public sealed class ContentDbContextFactory : IDesignTimeDbContextFactory<ContentDbContext>
{
    public ContentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=content;Username=postgres;Password=postgres")
            .Options;
        return new ContentDbContext(options);
    }
}
