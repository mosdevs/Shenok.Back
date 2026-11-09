using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shenok.Back.RecognitionService.Infrastructure.Persistence;
using Shenok.Back.RecognitionService.Infrastructure.Persistence.Configurations;

namespace Shenok.Back.RecognitionService.Infrastructure.Tests;

[Collection(InfrastructureFixture.Name)]
public sealed class SchemaTests(InfrastructureFixture fixture)
{
    [RequiresPostgresFact]
    public async Task Migration_creates_the_recognition_schema_and_is_idempotent()
    {
        await using (var db = fixture.CreateContext())
        {
            await db.Database.MigrateAsync();
            await db.Database.MigrateAsync();
        }

        var tables = await QueryStringsAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'");
        Assert.Contains(WaveformIndexEntryConfiguration.TableName, tables);
        Assert.Contains(InboxMessageConfiguration.TableName, tables);

        var primaryKeys = await QueryStringsAsync(
            """
            SELECT con.conname
            FROM pg_constraint con
            JOIN pg_class rel ON rel.oid = con.conrelid
            JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
            WHERE nsp.nspname = 'public' AND con.contype = 'p'
            """);
        Assert.Contains(WaveformIndexEntryConfiguration.PrimaryKeyName, primaryKeys);
        Assert.Contains(InboxMessageConfiguration.PrimaryKeyName, primaryKeys);

        var checks = await QueryStringsAsync(
            """
            SELECT con.conname
            FROM pg_constraint con
            JOIN pg_class rel ON rel.oid = con.conrelid
            JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
            WHERE nsp.nspname = 'public' AND con.contype = 'c'
            """);
        Assert.Contains(WaveformIndexEntryConfiguration.CodeCheckName, checks);
        Assert.Contains(WaveformIndexEntryConfiguration.StatusCheckName, checks);
        Assert.Contains(WaveformIndexEntryConfiguration.TargetCheckName, checks);
        Assert.Contains(InboxMessageConfiguration.TypeCheckName, checks);

        var codeCheck = await QueryStringAsync(
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_waveform_index_code'");
        Assert.Contains("1099511627775", codeCheck, StringComparison.Ordinal);

        var messageTypeLength = await QueryIntAsync(
            """
            SELECT character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'inbox_messages' AND column_name = 'message_type'
            """);
        Assert.Equal(64, messageTypeLength);

        var receivedType = await QueryStringAsync(
            """
            SELECT data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'inbox_messages' AND column_name = 'received_on'
            """);
        Assert.Equal("timestamp with time zone", receivedType);

        var userXmin = await QueryStringsAsync(
            """
            SELECT table_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND column_name = 'xmin'
            """);
        Assert.Empty(userXmin);

        var systemXmin = await QueryIntAsync(
            """
            SELECT a.attnum
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND c.relname = 'waveform_index'
              AND a.attname = 'xmin'
              AND NOT a.attisdropped
            """);
        Assert.True(systemXmin < 0);
    }

    private async Task<List<string>> QueryStringsAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
            values.Add(reader.GetString(0));

        return values;
    }

    private async Task<string> QueryStringAsync(string sql)
    {
        var values = await QueryStringsAsync(sql);
        return Assert.Single(values);
    }

    private async Task<int> QueryIntAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return Convert.ToInt32(value);
    }
}
