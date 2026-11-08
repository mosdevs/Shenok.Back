using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shenok.Back.Application.Abstractions;
using Shenok.Back.EngagementService.Application.Scans.UseCases.RegisterScan;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.DomainEvents;
using Shenok.Back.EngagementService.Infrastructure.Persistence;

namespace Shenok.Back.EngagementService.Infrastructure.Tests;

[Collection(InfrastructureFixture.Name)]
public sealed class RegisterScanPersistenceTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Same_scan_id_stores_one_receipt_and_unlocks_once()
    {
        fixture.Events.Clear();
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var request = new RegisterScanRequest(
            "scan-" + Guid.NewGuid().ToString("N"),
            userId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            pageId);

        await using (var scope = fixture.CreateScope())
        {
            var useCase = scope.ServiceProvider.GetRequiredService<IUseCase<RegisterScanRequest, RegisterScanResponse>>();
            var first = await useCase.ExecuteAsync(request, CancellationToken.None);
            Assert.True(first.Unlocked);
            Assert.False(first.IsFollowing);
            Assert.True(first.PromptFollow);
        }

        var unlockedAt = await ReadUnlockedAtAsync(userId, pageId);
        Assert.NotNull(unlockedAt);
        var unlocks = fixture.Events.Events.OfType<PageUnlockedDomainEvent>().ToList();
        Assert.Single(unlocks);

        RegisterScanResponse second;
        await using (var scope = fixture.CreateScope())
        {
            var useCase = scope.ServiceProvider.GetRequiredService<IUseCase<RegisterScanRequest, RegisterScanResponse>>();
            second = await useCase.ExecuteAsync(request, CancellationToken.None);
        }

        Assert.Equal(new RegisterScanResponse(true, false, true, 0), second);
        Assert.Equal(unlockedAt, await ReadUnlockedAtAsync(userId, pageId));
        Assert.Single(fixture.Events.Events.OfType<PageUnlockedDomainEvent>());

        await using var read = fixture.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<EngagementDbContext>();
        var scanId = ScanId.From(request.ScanId);
        var user = UserId.From(userId);
        var page = PageId.From(pageId);
        Assert.Equal(1, await db.ScanReceipts.CountAsync(receipt => receipt.Id == scanId));
        Assert.Equal(
            1,
            await db.UserPageRelationships.CountAsync(relationship =>
                relationship.UserId == user && relationship.PageId == page));
    }

    [Fact]
    public async Task MigrateOnStartup_false_does_not_create_tables()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await using var services = fixture.CreateServices(connectionString, migrateOnStartup: false);
        await EngagementDatabase.MigrateAsync(services);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'scan_receipts'",
            connection);
        var count = Convert.ToInt64(await command.ExecuteScalarAsync());
        Assert.Equal(0, count);
    }

    [Fact]
    public void Synchronous_save_throws()
    {
        using var db = fixture.CreateBareContext();
        var exception = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        Assert.Equal("Engagement changes must be saved asynchronously.", exception.Message);
    }

    private async Task<DateTime?> ReadUnlockedAtAsync(Guid userId, Guid pageId)
    {
        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EngagementDbContext>();
        var user = UserId.From(userId);
        var page = PageId.From(pageId);
        return await db.UserPageRelationships.AsNoTracking()
            .Where(relationship => relationship.UserId == user && relationship.PageId == page)
            .Select(relationship => relationship.UnlockedAt)
            .SingleAsync();
    }
}
