using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Domain.Repositories;
using Shenok.Back.EngagementService.Infrastructure.Persistence;

namespace Shenok.Back.EngagementService.Infrastructure.Tests;

[Collection(InfrastructureFixture.Name)]
public sealed class PersistenceConflictTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Lost_scan_insert_detaches_the_attempt_and_reloads_the_winner()
    {
        var scanId = ScanId.From("scan-" + Guid.NewGuid().ToString("N"));
        var winner = UserId.New();
        var otherUser = UserId.New();

        await using (var scope = fixture.CreateScope())
        {
            var receipts = scope.ServiceProvider.GetRequiredService<IScanReceiptRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await receipts.AddAsync(Receipt(scanId, winner), CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using var loserScope = fixture.CreateScope();
        var loserReceipts = loserScope.ServiceProvider.GetRequiredService<IScanReceiptRepository>();
        var loserUnitOfWork = loserScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var loser = Receipt(scanId, otherUser);
        var otherScan = ScanId.From("scan-" + Guid.NewGuid().ToString("N"));
        var other = Receipt(otherScan, otherUser);
        await loserReceipts.AddAsync(loser, CancellationToken.None);
        await loserReceipts.AddAsync(other, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<EngagementInsertConflictException>(() =>
            loserUnitOfWork.SaveChangesAsync(CancellationToken.None));
        Assert.Equal(EngagementInsertTarget.ScanReceipt, exception.Target);

        var loaded = await loserReceipts.GetAsync(scanId, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.True(loaded.BelongsTo(winner));
        Assert.False(loaded.BelongsTo(otherUser));

        await loserReceipts.AddAsync(other, CancellationToken.None);
        await loserUnitOfWork.SaveChangesAsync(CancellationToken.None);

        await using var read = fixture.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<EngagementDbContext>();
        Assert.Equal(1, await db.ScanReceipts.CountAsync(receipt => receipt.Id == scanId));
        Assert.Equal(1, await db.ScanReceipts.CountAsync(receipt => receipt.Id == otherScan));
    }

    [Fact]
    public async Task Lost_xmin_update_reloads_the_committed_row()
    {
        var userId = UserId.New();
        var pageId = PageId.New();

        await using (var scope = fixture.CreateScope())
        {
            var relationships = scope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await relationships.AddAsync(UserPageRelationship.Start(userId, pageId), CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using var winnerScope = fixture.CreateScope();
        await using var loserScope = fixture.CreateScope();
        var winnerRepository = winnerScope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
        var loserRepository = loserScope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
        var winnerUnitOfWork = winnerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var loserUnitOfWork = loserScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var winner = await winnerRepository.GetAsync(userId, pageId, CancellationToken.None);
        var stale = await loserRepository.GetAsync(userId, pageId, CancellationToken.None);
        Assert.NotNull(winner);
        Assert.NotNull(stale);
        Assert.True(winner.Follow());
        Assert.True(stale.Follow());
        await winnerRepository.UpdateAsync(winner, CancellationToken.None);
        await winnerUnitOfWork.SaveChangesAsync(CancellationToken.None);

        await loserRepository.UpdateAsync(stale, CancellationToken.None);
        await Assert.ThrowsAsync<EngagementConcurrencyException>(() =>
            loserUnitOfWork.SaveChangesAsync(CancellationToken.None));

        var reloaded = await loserRepository.GetAsync(userId, pageId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.NotSame(stale, reloaded);
        Assert.True(reloaded.IsFollowing);
        Assert.True(reloaded.Unfollow());
        await loserRepository.UpdateAsync(reloaded, CancellationToken.None);
        await loserUnitOfWork.SaveChangesAsync(CancellationToken.None);

        await using var read = fixture.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<EngagementDbContext>();
        var stored = await db.UserPageRelationships.AsNoTracking()
            .SingleAsync(relationship => relationship.UserId == userId && relationship.PageId == pageId);
        Assert.False(stored.IsFollowing);
        Assert.Null(stored.FollowedAt);
        Assert.Null(stored.UnlockedAt);
    }

    private static ScanReceipt Receipt(ScanId scanId, UserId userId) =>
        ScanReceipt.Record(
            scanId,
            userId,
            WaveformId.New(),
            VoiceId.New(),
            PageId.New(),
            unlocked: true,
            isFollowing: false,
            promptFollow: true,
            PlaybackPosition.Zero);
}
