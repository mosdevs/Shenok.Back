using Shenok.Back.ContentService.Domain.Aggregates.Voices;
using Shenok.Back.ContentService.Infrastructure.Packaging;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Packaging;

/// <summary>
/// The packager reads this queue. A voice that is not on its page is left alone,
/// except a pending voice on a published page, which cannot be listed until it is Ready.
/// </summary>
[Collection(InfrastructureFixture.Name)]
public sealed class PackagingQueueTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Queue_returns_listed_pending_voices_and_pending_voices_on_a_published_page()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        await fixture.MigrateAsync(connectionString);

        var draft = ContentFactory.DraftPage();
        var listed = ContentFactory.PendingVoice(draft.Id);
        var omitted = ContentFactory.PendingVoice(draft.Id);
        var failed = ContentFactory.FailedVoice(draft.Id);
        draft.AddVoiceReference(listed.Id, listed.Status);
        draft.AddVoiceReference(failed.Id, failed.Status);

        var published = ContentFactory.DraftPage();
        var ready = ContentFactory.ReadyVoice(published.Id);
        var waiting = ContentFactory.PendingVoice(published.Id);
        published.AddVoiceReference(ready.Id, ready.Status);
        published.Publish(new Dictionary<VoiceId, PackagingStatus> { [ready.Id] = ready.Status });

        var archived = ContentFactory.DraftPage();
        var kept = ContentFactory.PendingVoice(archived.Id);
        var dropped = ContentFactory.PendingVoice(archived.Id);
        archived.AddVoiceReference(kept.Id, kept.Status);
        archived.Archive();

        await using (var write = fixture.CreateBareContext(connectionString))
        {
            write.AddRange(draft, listed, omitted, failed, published, ready, waiting, archived, kept, dropped);
            await write.SaveChangesAsync();
        }

        await using var db = fixture.CreateBareContext(connectionString);
        var selected = await PackagingQueue.ListAsync(db, batchSize: 10, CancellationToken.None);
        var ids = selected.Select(id => id.Value).ToHashSet();

        Assert.Equal(3, ids.Count);
        Assert.Contains(listed.Id.Value, ids);
        Assert.Contains(waiting.Id.Value, ids);
        Assert.Contains(kept.Id.Value, ids);
        Assert.DoesNotContain(omitted.Id.Value, ids);
        Assert.DoesNotContain(failed.Id.Value, ids);
        Assert.DoesNotContain(ready.Id.Value, ids);
        Assert.DoesNotContain(dropped.Id.Value, ids);

        var first = await PackagingQueue.ListAsync(db, batchSize: 1, CancellationToken.None);
        Assert.Single(first);
        Assert.Contains(first[0].Value, ids);
    }

    [Fact]
    public async Task Queue_rejects_an_empty_batch()
    {
        await using var db = fixture.CreateBareContext();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => PackagingQueue.ListAsync(db, 0, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => PackagingQueue.ListAsync(null!, 1, CancellationToken.None));
    }
}
