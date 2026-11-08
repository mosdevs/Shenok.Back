using Npgsql;
using Shenok.Back.EngagementService.Infrastructure.Persistence;
using Shenok.Back.EngagementService.Infrastructure.Persistence.Configurations;

namespace Shenok.Back.EngagementService.Infrastructure.Tests;

[Collection(InfrastructureFixture.Name)]
public sealed class SchemaConstraintTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Migration_creates_the_engagement_schema_and_is_idempotent()
    {
        await using var services = fixture.CreateServices(fixture.PostgresConnectionString, migrateOnStartup: true);
        await EngagementDatabase.MigrateAsync(services);
        await EngagementDatabase.MigrateAsync(services);

        var tables = await QueryStringsAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'");
        Assert.Contains(UserPageRelationshipConfiguration.TableName, tables);
        Assert.Contains(ListeningRecordConfiguration.TableName, tables);
        Assert.Contains(ScanReceiptConfiguration.TableName, tables);
        Assert.Contains(PlaySessionConfiguration.TableName, tables);
        Assert.DoesNotContain("outbox_messages", tables);

        var primaryKeys = await QueryStringsAsync(
            """
            SELECT con.conname
            FROM pg_constraint con
            JOIN pg_class rel ON rel.oid = con.conrelid
            JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
            WHERE nsp.nspname = 'public' AND con.contype = 'p'
            """);
        Assert.Contains(UserPageRelationshipConfiguration.PrimaryKeyName, primaryKeys);
        Assert.Contains(ListeningRecordConfiguration.PrimaryKeyName, primaryKeys);
        Assert.Contains(ScanReceiptConfiguration.PrimaryKeyName, primaryKeys);
        Assert.Contains(PlaySessionConfiguration.PrimaryKeyName, primaryKeys);

        var checks = await QueryStringsAsync(
            """
            SELECT con.conname
            FROM pg_constraint con
            JOIN pg_class rel ON rel.oid = con.conrelid
            JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
            WHERE nsp.nspname = 'public' AND con.contype = 'c'
            """);
        Assert.Contains(ListeningRecordConfiguration.PositionCheckName, checks);
        Assert.Contains(ScanReceiptConfiguration.ResumePositionCheckName, checks);

        var userXminColumns = await QueryStringsAsync(
            """
            SELECT table_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND column_name = 'xmin'
            """);
        Assert.Empty(userXminColumns);

        var systemXmin = await QueryIntsAsync(
            """
            SELECT a.attnum
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND c.relname = ANY(@tables)
              AND a.attname = 'xmin'
              AND NOT a.attisdropped
            """,
            new NpgsqlParameter("tables", new[]
            {
                UserPageRelationshipConfiguration.TableName,
                ListeningRecordConfiguration.TableName,
                PlaySessionConfiguration.TableName
            }));
        Assert.Equal(3, systemXmin.Count);
        Assert.All(systemXmin, attnum => Assert.True(attnum < 0));
    }

    [Fact]
    public async Task Database_rejects_a_negative_playback_position()
    {
        await RejectAsync(
            """
            INSERT INTO listening_records (user_id, voice_id, position, is_completed)
            VALUES (@user, @voice, -1, false)
            """,
            new NpgsqlParameter("user", Guid.NewGuid()),
            new NpgsqlParameter("voice", Guid.NewGuid()));

        await RejectAsync(
            """
            INSERT INTO scan_receipts (
                scan_id, user_id, waveform_id, voice_id, page_id, created_at,
                unlocked, is_following, prompt_follow, resume_position)
            VALUES (@scan, @user, @waveform, @voice, @page, now(), false, false, true, -1)
            """,
            new NpgsqlParameter("scan", "scan-" + Guid.NewGuid().ToString("N")),
            new NpgsqlParameter("user", Guid.NewGuid()),
            new NpgsqlParameter("waveform", Guid.NewGuid()),
            new NpgsqlParameter("voice", Guid.NewGuid()),
            new NpgsqlParameter("page", Guid.NewGuid()));
    }

    private async Task RejectAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
            command.Parameters.Add(parameter);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        await transaction.RollbackAsync();
    }

    private async Task<IReadOnlyList<string>> QueryStringsAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            values.Add(reader.GetString(0));

        return values;
    }

    private async Task<IReadOnlyList<int>> QueryIntsAsync(string sql, NpgsqlParameter parameter)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(parameter);
        var values = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            values.Add(Convert.ToInt32(reader.GetValue(0)));

        return values;
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
