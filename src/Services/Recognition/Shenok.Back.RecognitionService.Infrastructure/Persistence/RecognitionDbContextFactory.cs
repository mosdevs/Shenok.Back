using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shenok.Back.RecognitionService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c>. The running host uses <c>ConnectionStrings:recognition</c>.
/// </summary>
public sealed class RecognitionDbContextFactory : IDesignTimeDbContextFactory<RecognitionDbContext>
{
    public RecognitionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RecognitionDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=recognition;Username=postgres;Password=postgres")
            .Options;
        return new RecognitionDbContext(options);
    }
}
