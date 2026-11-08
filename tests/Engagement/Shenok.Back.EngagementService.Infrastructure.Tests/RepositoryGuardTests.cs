using Microsoft.EntityFrameworkCore;
using Shenok.Back.EngagementService.Domain.Aggregates.ListeningRecords;
using Shenok.Back.EngagementService.Domain.Aggregates.PlaySessions;
using Shenok.Back.EngagementService.Domain.Aggregates.ScanReceipts;
using Shenok.Back.EngagementService.Domain.Aggregates.UserPageRelationships;
using Shenok.Back.EngagementService.Infrastructure.Persistence;
using Shenok.Back.EngagementService.Infrastructure.Persistence.Repositories;

namespace Shenok.Back.EngagementService.Infrastructure.Tests;

public sealed class RepositoryGuardTests
{
    [Fact]
    public async Task Repositories_reject_missing_arguments_and_an_empty_voice_list()
    {
        var options = new DbContextOptionsBuilder<EngagementDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=engagement;Username=engagement;Password=engagement")
            .Options;
        await using var db = new EngagementDbContext(options);
        var relationships = new UserPageRelationshipRepository(db);
        var records = new ListeningRecordRepository(db);
        var receipts = new ScanReceiptRepository(db);
        var sessions = new PlaySessionRepository(db);
        var userId = UserId.New();
        var pageId = PageId.New();
        var voiceId = VoiceId.New();

        await Assert.ThrowsAsync<ArgumentNullException>(() => relationships.GetAsync(null!, pageId));
        await Assert.ThrowsAsync<ArgumentNullException>(() => relationships.GetAsync(userId, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => relationships.AddAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => relationships.UpdateAsync(null!));

        await Assert.ThrowsAsync<ArgumentNullException>(() => records.GetAsync(null!, voiceId));
        await Assert.ThrowsAsync<ArgumentNullException>(() => records.GetAsync(userId, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => records.ListAsync(null!, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => records.ListAsync(userId, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => records.AddAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => records.UpdateAsync(null!));
        Assert.Empty(await records.ListAsync(userId, []));

        await Assert.ThrowsAsync<ArgumentNullException>(() => receipts.GetAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => receipts.AddAsync(null!));

        await Assert.ThrowsAsync<ArgumentNullException>(() => sessions.GetAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sessions.AddAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sessions.UpdateAsync(null!));
    }
}
