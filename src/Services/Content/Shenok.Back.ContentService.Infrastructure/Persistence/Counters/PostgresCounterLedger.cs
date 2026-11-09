using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shenok.Back.Application.Abstractions.Exceptions;
using Shenok.Back.ContentService.Application.Common.Interfaces;

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Counters;

/// <summary>
/// Inserts a receipt, then updates one counter row, in one transaction.
/// A duplicate key does not add again. A subscriber update never goes below zero.
/// </summary>
public sealed class PostgresCounterLedger(ContentDbContext db) : ICounterLedger
{
    public Task<SubscriberCounterUpdate> ApplySubscriberDeltaAsync(
        Guid pageId,
        int delta,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (delta is not (1 or -1))
            throw new ArgumentOutOfRangeException(nameof(delta), "Subscriber delta must be 1 or -1.");

        return InTransactionAsync(
            async (connection, transaction, token) =>
            {
                var locked = await ScalarAsync(
                    connection,
                    transaction,
                    """
                    SELECT subscriber_count
                    FROM page_counters
                    WHERE page_id = @page_id
                    FOR UPDATE
                    """,
                    token,
                    ("page_id", pageId));
                if (locked is null)
                    return new SubscriberCounterUpdate(false, false, 0);

                var inserted = await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO subscriber_counter_receipts (idempotency_key, page_id, delta, created_at)
                    VALUES (@idempotency_key, @page_id, @delta, now())
                    ON CONFLICT (idempotency_key) DO NOTHING
                    """,
                    token,
                    ("idempotency_key", idempotencyKey),
                    ("page_id", pageId),
                    ("delta", delta));
                if (inserted == 0)
                {
                    var stored = await ReadReceiptAsync(connection, transaction, idempotencyKey, token);
                    if (stored is null)
                        throw new InvalidOperationException("The subscriber receipt disappeared after a conflict.");
                    if (stored.Value.TargetId != pageId || stored.Value.Delta != delta)
                        throw new ConflictException("Idempotency key is already used.");

                    return new SubscriberCounterUpdate(true, false, locked.Value);
                }

                var bumped = await ScalarAsync(
                    connection,
                    transaction,
                    """
                    UPDATE page_counters
                    SET subscriber_count = subscriber_count + @delta
                    WHERE page_id = @page_id
                      AND subscriber_count + @delta >= 0
                    RETURNING subscriber_count
                    """,
                    token,
                    ("page_id", pageId),
                    ("delta", delta));
                return new SubscriberCounterUpdate(true, true, bumped ?? locked.Value);
            },
            cancellationToken);
    }

    public Task<PlayCounterUpdate> IncrementPlayAsync(
        Guid voiceId,
        string playSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playSessionId);

        return InTransactionAsync(
            async (connection, transaction, token) =>
            {
                var locked = await ScalarAsync(
                    connection,
                    transaction,
                    """
                    SELECT play_count
                    FROM voice_counters
                    WHERE voice_id = @voice_id
                    FOR UPDATE
                    """,
                    token,
                    ("voice_id", voiceId));
                if (locked is null)
                    return new PlayCounterUpdate(false, false, 0);

                var inserted = await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO play_counter_receipts (idempotency_key, voice_id, created_at)
                    VALUES (@idempotency_key, @voice_id, now())
                    ON CONFLICT (idempotency_key) DO NOTHING
                    """,
                    token,
                    ("idempotency_key", playSessionId),
                    ("voice_id", voiceId));
                if (inserted == 0)
                {
                    var stored = await ReadReceiptAsync(connection, transaction, playSessionId, token, plays: true);
                    if (stored is null)
                        throw new InvalidOperationException("The play receipt disappeared after a conflict.");
                    if (stored.Value.TargetId != voiceId)
                        throw new ConflictException("Idempotency key is already used.");

                    return new PlayCounterUpdate(true, false, locked.Value);
                }

                var bumped = await ScalarAsync(
                    connection,
                    transaction,
                    """
                    UPDATE voice_counters
                    SET play_count = play_count + 1
                    WHERE voice_id = @voice_id
                    RETURNING play_count
                    """,
                    token,
                    ("voice_id", voiceId));
                return new PlayCounterUpdate(true, true, bumped ?? locked.Value);
            },
            cancellationToken);
    }

    private async Task<T> InTransactionAsync<T>(
        Func<DbConnection, DbTransaction, CancellationToken, Task<T>> execute,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await execute(
                db.Database.GetDbConnection(),
                transaction.GetDbTransaction(),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static async Task<long?> ScalarAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = Create(connection, transaction, sql, parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null or DBNull)
            return null;

        return Convert.ToInt64(value);
    }

    private static async Task<int> ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = Create(connection, transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(Guid TargetId, int Delta)?> ReadReceiptAsync(
        DbConnection connection,
        DbTransaction transaction,
        string idempotencyKey,
        CancellationToken cancellationToken,
        bool plays = false)
    {
        var sql = plays
            ? """
              SELECT voice_id, 1
              FROM play_counter_receipts
              WHERE idempotency_key = @idempotency_key
              """
            : """
              SELECT page_id, delta
              FROM subscriber_counter_receipts
              WHERE idempotency_key = @idempotency_key
              """;
        await using var command = Create(connection, transaction, sql, ("idempotency_key", idempotencyKey));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return (reader.GetGuid(0), reader.GetInt32(1));
    }

    private static DbCommand Create(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return command;
    }
}
