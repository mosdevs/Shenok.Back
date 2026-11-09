using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shenok.Back.ContentService.Infrastructure.Messaging;
using Shenok.Back.ContentService.Infrastructure.Persistence;
using Shenok.Back.ContentService.Infrastructure.Persistence.Outbox;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Messaging;

[Collection(InfrastructureFixture.Name)]
public sealed class OutboxPublisherTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task One_pass_publishes_unpublished_rows_in_occurred_on_order()
    {
        var connection = await FreshDatabaseAsync();
        var earlier = OutboxMessage.From(ContentFactory.PageMapping(ContentFactory.DraftPage().Id).DomainEvents.Single())!;
        var later = OutboxMessage.From(ContentFactory.PageMapping(ContentFactory.DraftPage().Id).DomainEvents.Single())!;
        await StampOrderAsync(connection, earlier, later);

        var publisher = new RecordingPublisher();
        await using var services = Build(connection, publisher);
        var published = await services.GetRequiredService<OutboxPublisher>().PublishPendingAsync(CancellationToken.None);

        Assert.Equal(2, published);
        Assert.Equal([earlier.Id, later.Id], publisher.Messages.Select(message => message.EventId).ToArray());
        await using var read = fixture.CreateBareContext(connection);
        var rows = await read.OutboxMessages.AsNoTracking().OrderBy(message => message.OccurredOn).ThenBy(message => message.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(rows.Select(row => row.Payload), publisher.Messages.Select(message => message.Payload));
        Assert.All(rows, row =>
        {
            Assert.NotNull(row.PublishedOn);
            Assert.Equal(DateTimeKind.Utc, row.PublishedOn!.Value.Kind);
        });
    }

    [Fact]
    public async Task A_second_pass_publishes_nothing()
    {
        var connection = await FreshDatabaseAsync();
        var row = OutboxMessage.From(ContentFactory.PageMapping(ContentFactory.DraftPage().Id).DomainEvents.Single())!;
        await using (var db = fixture.CreateBareContext(connection))
        {
            db.OutboxMessages.Add(row);
            await db.SaveChangesAsync();
        }

        var publisher = new RecordingPublisher();
        await using var services = Build(connection, publisher);
        var worker = services.GetRequiredService<OutboxPublisher>();

        Assert.Equal(1, await worker.PublishPendingAsync(CancellationToken.None));
        Assert.Equal(0, await worker.PublishPendingAsync(CancellationToken.None));
        Assert.Single(publisher.Messages);
    }

    [Fact]
    public async Task A_broker_failure_leaves_the_batch_unpublished()
    {
        var connection = await FreshDatabaseAsync();
        var earlier = OutboxMessage.From(ContentFactory.PageMapping(ContentFactory.DraftPage().Id).DomainEvents.Single())!;
        var later = OutboxMessage.From(ContentFactory.PageMapping(ContentFactory.DraftPage().Id).DomainEvents.Single())!;
        await StampOrderAsync(connection, earlier, later);

        var publisher = new RecordingPublisher { FailuresRemaining = 1 };
        await using var services = Build(connection, publisher);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            services.GetRequiredService<OutboxPublisher>().PublishPendingAsync(CancellationToken.None));

        Assert.Empty(publisher.Messages);
        await using var read = fixture.CreateBareContext(connection);
        var rows = await read.OutboxMessages.AsNoTracking().ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Null(row.PublishedOn));
    }

    private async Task<string> FreshDatabaseAsync()
    {
        var connection = await fixture.CreateDatabaseAsync();
        await fixture.MigrateAsync(connection);
        return connection;
    }

    private async Task StampOrderAsync(string connection, OutboxMessage earlier, OutboxMessage later)
    {
        var first = new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc);
        var second = first.AddMinutes(5);
        await using var db = fixture.CreateBareContext(connection);
        db.OutboxMessages.Add(earlier);
        db.OutboxMessages.Add(later);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE outbox_messages SET occurred_on = {first} WHERE id = {earlier.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE outbox_messages SET occurred_on = {second} WHERE id = {later.Id}");
    }

    private static ServiceProvider Build(string connection, RecordingPublisher publisher)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ContentDbContext>(options => options.UseNpgsql(connection));
        services.AddSingleton<IWaveformEventPublisher>(publisher);
        services.AddSingleton(Options.Create(new MessagingOptions
        {
            Enabled = true,
            PollIntervalSeconds = 1,
            BatchSize = 20
        }));
        services.AddSingleton<OutboxPublisher>();
        return services.BuildServiceProvider();
    }

    private sealed class RecordingPublisher : IWaveformEventPublisher
    {
        public List<(Guid EventId, string MessageType, string Payload)> Messages { get; } = [];

        public int FailuresRemaining { get; set; }

        public Task PublishAsync(Guid eventId, string messageType, string payload, CancellationToken cancellationToken)
        {
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new InvalidOperationException("The broker nacked the waveform event.");
            }

            Messages.Add((eventId, messageType, payload));
            return Task.CompletedTask;
        }
    }
}
