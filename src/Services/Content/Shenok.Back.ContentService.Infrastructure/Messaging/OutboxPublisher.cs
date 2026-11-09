using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Shenok.Back.ContentService.Infrastructure.Persistence;
using Shenok.Back.ContentService.Infrastructure.Persistence.Outbox;

namespace Shenok.Back.ContentService.Infrastructure.Messaging;

/// <summary>
/// Claims unpublished waveform rows and marks them only after the broker confirms.
/// A throw rolls the transaction back. The row stays unpublished. A later delivery is safe
/// because Recognition records the event id.
/// </summary>
internal sealed class OutboxPublisher(
    IServiceScopeFactory scopes,
    IOptions<MessagingOptions> options,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var messaging = options.Value;
        if (!MessagingOptions.IsValid(messaging))
            throw new InvalidOperationException("Messaging configuration is invalid.");

        var interval = TimeSpan.FromSeconds(messaging.PollIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await PublishPendingAsync(stoppingToken);
                if (published == 0)
                    await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Waveform outbox publish failed.");
                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    internal async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
    {
        var batch = options.Value.BatchSize;
        if (batch <= 0)
            throw new InvalidOperationException("Messaging:BatchSize must be greater than zero.");

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IWaveformEventPublisher>();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var ids = await ClaimAsync(db, batch, cancellationToken);
        if (ids.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var rows = await db.OutboxMessages
            .AsTracking()
            .Where(message => ids.Contains(message.Id))
            .ToListAsync(cancellationToken);

        foreach (var row in rows.OrderBy(message => message.OccurredOn).ThenBy(message => message.Id))
        {
            await publisher.PublishAsync(row.Id, row.Type, row.Payload, cancellationToken);
            row.MarkPublished(DateTime.UtcNow);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Count;
    }

    private static async Task<List<Guid>> ClaimAsync(ContentDbContext db, int batch, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("The outbox claim requires a transaction.");
        command.CommandText =
            """
            SELECT id
            FROM outbox_messages
            WHERE published_on IS NULL
            ORDER BY occurred_on, id
            LIMIT @batch
            FOR UPDATE SKIP LOCKED
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "batch";
        parameter.Value = batch;
        command.Parameters.Add(parameter);

        var ids = new List<Guid>(batch);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            ids.Add(reader.GetGuid(0));

        return ids;
    }
}
