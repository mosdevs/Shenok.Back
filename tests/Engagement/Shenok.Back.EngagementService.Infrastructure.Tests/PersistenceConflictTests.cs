using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shenok.Back.EngagementService.Application.Common;
using Shenok.Back.EngagementService.Application.Common.Interfaces;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
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

    [Fact]
    public async Task Lost_relationship_insert_detaches_the_attempt_and_reloads_the_winner()
    {
        var userId = UserId.New();
        var pageId = PageId.New();

        await using (var scope = fixture.CreateScope())
        {
            var relationships = scope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var winner = UserPageRelationship.Start(userId, pageId);
            Assert.True(winner.Unlock());
            await relationships.AddAsync(winner, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using var loserScope = fixture.CreateScope();
        var loserRelationships = loserScope.ServiceProvider.GetRequiredService<IUserPageRelationshipRepository>();
        var loserUnitOfWork = loserScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var loser = UserPageRelationship.Start(userId, pageId);
        Assert.True(loser.Follow());
        var otherPage = PageId.New();
        var other = UserPageRelationship.Start(userId, otherPage);
        await loserRelationships.AddAsync(loser, CancellationToken.None);
        await loserRelationships.AddAsync(other, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<EngagementInsertConflictException>(() =>
            loserUnitOfWork.SaveChangesAsync(CancellationToken.None));
        Assert.Equal(EngagementInsertTarget.UserPageRelationship, exception.Target);

        var loaded = await loserRelationships.GetAsync(userId, pageId, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.NotSame(loser, loaded);
        Assert.NotNull(loaded.UnlockedAt);
        Assert.False(loaded.IsFollowing);

        await loserRelationships.AddAsync(other, CancellationToken.None);
        await loserUnitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Lost_listening_insert_detaches_the_attempt_and_reloads_the_winner()
    {
        var userId = UserId.New();
        var voiceId = VoiceId.New();

        await using (var scope = fixture.CreateScope())
        {
            var records = scope.ServiceProvider.GetRequiredService<IListeningRecordRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var winner = ListeningRecord.Start(userId, voiceId);
            Assert.True(winner.ReportProgress(PlaybackPosition.From(4), completed: false, isSeek: false).Accepted);
            await records.AddAsync(winner, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using var loserScope = fixture.CreateScope();
        var loserRecords = loserScope.ServiceProvider.GetRequiredService<IListeningRecordRepository>();
        var loserUnitOfWork = loserScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var loser = ListeningRecord.Start(userId, voiceId);
        Assert.True(loser.ReportProgress(PlaybackPosition.From(9), completed: false, isSeek: false).Accepted);
        var other = ListeningRecord.Start(userId, VoiceId.New());
        Assert.True(other.ReportProgress(PlaybackPosition.From(1), completed: false, isSeek: false).Accepted);
        await loserRecords.AddAsync(loser, CancellationToken.None);
        await loserRecords.AddAsync(other, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<EngagementInsertConflictException>(() =>
            loserUnitOfWork.SaveChangesAsync(CancellationToken.None));
        Assert.Equal(EngagementInsertTarget.ListeningRecord, exception.Target);

        var loaded = await loserRecords.GetAsync(userId, voiceId, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.NotSame(loser, loaded);
        Assert.Equal(4, loaded.Position.Value);

        await loserRecords.AddAsync(other, CancellationToken.None);
        await loserUnitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Lost_play_session_insert_detaches_the_attempt_and_reloads_the_winner()
    {
        var sessionId = PlaySessionId.From("play-" + Guid.NewGuid().ToString("N"));
        var winnerUser = UserId.New();
        var winnerVoice = VoiceId.New();

        await using (var scope = fixture.CreateScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IPlaySessionRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await sessions.AddAsync(PlaySession.Open(sessionId, winnerUser, winnerVoice), CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using var loserScope = fixture.CreateScope();
        var loserSessions = loserScope.ServiceProvider.GetRequiredService<IPlaySessionRepository>();
        var loserUnitOfWork = loserScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var loser = PlaySession.Open(sessionId, UserId.New(), VoiceId.New());
        var other = PlaySession.Open(PlaySessionId.From("play-" + Guid.NewGuid().ToString("N")), UserId.New(), VoiceId.New());
        await loserSessions.AddAsync(loser, CancellationToken.None);
        await loserSessions.AddAsync(other, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<EngagementInsertConflictException>(() =>
            loserUnitOfWork.SaveChangesAsync(CancellationToken.None));
        Assert.Equal(EngagementInsertTarget.PlaySession, exception.Target);

        var loaded = await loserSessions.GetAsync(sessionId, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.NotSame(loser, loaded);
        Assert.True(loaded.BelongsTo(winnerUser, winnerVoice));
        Assert.False(loaded.BelongsTo(loser.UserId, loser.VoiceId));

        await loserSessions.AddAsync(other, CancellationToken.None);
        await loserUnitOfWork.SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Lost_listening_xmin_update_reloads_the_committed_position()
    {
        var userId = UserId.New();
        var voiceId = VoiceId.New();

        await using (var scope = fixture.CreateScope())
        {
            var records = scope.ServiceProvider.GetRequiredService<IListeningRecordRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var created = ListeningRecord.Start(userId, voiceId);
            Assert.True(created.ReportProgress(PlaybackPosition.From(2), completed: false, isSeek: false).Accepted);
            await records.AddAsync(created, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using var winnerScope = fixture.CreateScope();
        await using var loserScope = fixture.CreateScope();
        var winnerRepository = winnerScope.ServiceProvider.GetRequiredService<IListeningRecordRepository>();
        var loserRepository = loserScope.ServiceProvider.GetRequiredService<IListeningRecordRepository>();
        var winnerUnitOfWork = winnerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var loserUnitOfWork = loserScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var winner = await winnerRepository.GetAsync(userId, voiceId, CancellationToken.None);
        var stale = await loserRepository.GetAsync(userId, voiceId, CancellationToken.None);
        Assert.NotNull(winner);
        Assert.NotNull(stale);
        Assert.True(winner.ReportProgress(PlaybackPosition.From(8), completed: false, isSeek: false).Accepted);
        Assert.True(stale.ReportProgress(PlaybackPosition.From(15), completed: false, isSeek: false).Accepted);
        await winnerRepository.UpdateAsync(winner, CancellationToken.None);
        await winnerUnitOfWork.SaveChangesAsync(CancellationToken.None);

        await loserRepository.UpdateAsync(stale, CancellationToken.None);
        await Assert.ThrowsAsync<EngagementConcurrencyException>(() =>
            loserUnitOfWork.SaveChangesAsync(CancellationToken.None));

        var reloaded = await loserRepository.GetAsync(userId, voiceId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.NotSame(stale, reloaded);
        Assert.Equal(8, reloaded.Position.Value);
        Assert.True(reloaded.ReportProgress(PlaybackPosition.From(20), completed: false, isSeek: false).Accepted);
        await loserRepository.UpdateAsync(reloaded, CancellationToken.None);
        await loserUnitOfWork.SaveChangesAsync(CancellationToken.None);

        await using var read = fixture.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<EngagementDbContext>();
        var stored = await db.ListeningRecords.AsNoTracking()
            .SingleAsync(record => record.UserId == userId && record.VoiceId == voiceId);
        Assert.Equal(20, stored.Position.Value);
    }

    [Fact]
    public async Task Lost_play_session_xmin_update_reloads_the_counted_row()
    {
        var sessionId = PlaySessionId.From("play-" + Guid.NewGuid().ToString("N"));
        var userId = UserId.New();
        var voiceId = VoiceId.New();

        await using (var scope = fixture.CreateScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IPlaySessionRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await sessions.AddAsync(PlaySession.Open(sessionId, userId, voiceId), CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using var winnerScope = fixture.CreateScope();
        await using var loserScope = fixture.CreateScope();
        var winnerRepository = winnerScope.ServiceProvider.GetRequiredService<IPlaySessionRepository>();
        var loserRepository = loserScope.ServiceProvider.GetRequiredService<IPlaySessionRepository>();
        var winnerUnitOfWork = winnerScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var loserUnitOfWork = loserScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var winner = await winnerRepository.GetAsync(sessionId, CancellationToken.None);
        var stale = await loserRepository.GetAsync(sessionId, CancellationToken.None);
        Assert.NotNull(winner);
        Assert.NotNull(stale);
        Assert.True(winner.AcceptFirstReport());
        Assert.True(stale.AcceptFirstReport());
        await winnerRepository.UpdateAsync(winner, CancellationToken.None);
        await winnerUnitOfWork.SaveChangesAsync(CancellationToken.None);

        await loserRepository.UpdateAsync(stale, CancellationToken.None);
        await Assert.ThrowsAsync<EngagementConcurrencyException>(() =>
            loserUnitOfWork.SaveChangesAsync(CancellationToken.None));

        var reloaded = await loserRepository.GetAsync(sessionId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.NotSame(stale, reloaded);
        Assert.True(reloaded.Counted);
        Assert.False(reloaded.AcceptFirstReport());
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
