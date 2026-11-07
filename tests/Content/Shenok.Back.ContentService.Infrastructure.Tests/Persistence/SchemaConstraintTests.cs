using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shenok.Back.ContentService.Infrastructure.Persistence;
using Shenok.Back.ContentService.Domain.Aggregates.Counters;
using Shenok.Back.ContentService.Domain.Aggregates.WaveformMappings;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Persistence;

[Collection(InfrastructureFixture.Name)]
public sealed class SchemaConstraintTests(InfrastructureFixture fixture)
{
    [Fact]
    public async Task Migration_creates_the_content_schema_and_is_idempotent()
    {
        await fixture.MigrateAsync(fixture.PostgresConnectionString);
        var tables = await QueryStringsAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");
        var indexes = await QueryStringsAsync(
            "SELECT indexname FROM pg_indexes WHERE schemaname = 'public'");

        Assert.Contains("pages", tables);
        Assert.Contains("voices", tables);
        Assert.Contains("waveform_mappings", tables);
        Assert.Contains("page_counters", tables);
        Assert.Contains("voice_counters", tables);
        Assert.Contains("outbox_messages", tables);
        Assert.Contains("ix_voices_source_storage_key", indexes);
        Assert.Contains("ix_voices_playlist_storage_key", indexes);
        Assert.Contains("ix_voices_segment_prefix", indexes);
        Assert.Contains("ix_waveform_mappings_waveform_code", indexes);
        Assert.Contains("ix_outbox_messages_unpublished", indexes);

        var indexDef = await QueryStringAsync(
            "SELECT indexdef FROM pg_indexes WHERE indexname = 'ix_outbox_messages_unpublished'");
        Assert.Contains("published_on IS NULL", indexDef, StringComparison.OrdinalIgnoreCase);

        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT max_value, cycle FROM pg_sequences WHERE sequencename = 'waveform_code_seq'",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(WaveformCode.MaxValue, reader.GetInt64(0));
        Assert.False(reader.GetBoolean(1));
    }

    [Fact]
    public async Task Database_rejects_a_negative_subscriber_count_and_a_negative_play_count()
    {
        await RejectAsync(
            "INSERT INTO page_counters (page_id, subscriber_count) VALUES (@id, -1)",
            new NpgsqlParameter("id", Guid.NewGuid()));
        await RejectAsync(
            "INSERT INTO voice_counters (voice_id, play_count) VALUES (@id, -1)",
            new NpgsqlParameter("id", Guid.NewGuid()));
    }

    [Fact]
    public async Task Database_rejects_an_invalid_page_status_or_a_voice_order_that_is_not_an_array()
    {
        await RejectAsync(
            """
            INSERT INTO pages (id, title, description, owner_id, created_at, status, voice_ids)
            VALUES (@id, 'Night', 'Quiet', @owner, now(), 'Hidden', '[]'::jsonb)
            """,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("owner", Guid.NewGuid()));
        await RejectAsync(
            """
            INSERT INTO pages (id, title, description, owner_id, created_at, status, voice_ids)
            VALUES (@id, 'Night', 'Quiet', @owner, now(), 'Draft', '{"voice":1}'::jsonb)
            """,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("owner", Guid.NewGuid()));
    }

    [Fact]
    public async Task Database_rejects_a_voice_outside_the_size_or_packaging_rules()
    {
        await RejectAsync(VoiceSql(sizeBytes: 0, status: "Pending", duration: "NULL", playlist: "NULL", prefix: "NULL"));
        await RejectAsync(VoiceSql(sizeBytes: 104857601, status: "Pending", duration: "NULL", playlist: "NULL", prefix: "NULL"));
        await RejectAsync(VoiceSql(sizeBytes: 2048, status: "Ready", duration: "NULL", playlist: "NULL", prefix: "NULL"));
        await RejectAsync(VoiceSql(sizeBytes: 2048, status: "Pending", duration: "12", playlist: "NULL", prefix: "NULL"));
        await RejectAsync(VoiceSql(sizeBytes: 2048, status: "Broken", duration: "NULL", playlist: "NULL", prefix: "NULL"));
    }

    [Fact]
    public async Task Database_rejects_a_waveform_or_outbox_row_outside_its_check()
    {
        await RejectAsync(
            """
            INSERT INTO waveform_mappings
                (id, waveform_code, target_type, target_id, layout_version, registered_at, status)
            VALUES (@id, 0, 'Page', @target, 1, now(), 'Active')
            """,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("target", Guid.NewGuid()));
        await RejectAsync(
            """
            INSERT INTO waveform_mappings
                (id, waveform_code, target_type, target_id, layout_version, registered_at, status)
            VALUES (@id, 1, 'User', @target, 1, now(), 'Active')
            """,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("target", Guid.NewGuid()));
        await RejectAsync(
            """
            INSERT INTO outbox_messages (id, message_type, payload, occurred_on)
            VALUES (@id, 'PageCreated', '{}'::jsonb, now())
            """,
            new NpgsqlParameter("id", Guid.NewGuid()));
    }

    [Fact]
    public async Task Counter_rows_start_at_zero_and_have_no_increment_method()
    {
        var page = ContentFactory.DraftPage();
        var voice = ContentFactory.PendingVoice(page.Id);
        await using (var scope = fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
            db.Add(PageCounter.Create(page.Id));
            db.Add(VoiceCounter.Create(voice.Id));
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateBareContext();
        var pageCounter = await read.PageCounters.AsNoTracking().SingleAsync(counter => counter.PageId == page.Id);
        var voiceCounter = await read.VoiceCounters.AsNoTracking().SingleAsync(counter => counter.VoiceId == voice.Id);
        Assert.Equal(0, pageCounter.SubscriberCount);
        Assert.Equal(0, voiceCounter.PlayCount);
        Assert.DoesNotContain(
            typeof(PageCounter).GetMethods(),
            method => method.Name.Contains("Increment", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(VoiceCounter).GetMethods(),
            method => method.Name.Contains("Increment", StringComparison.Ordinal));
    }

    private static string VoiceSql(long sizeBytes, string status, string duration, string playlist, string prefix) =>
        $"""
        INSERT INTO voices (
            id, page_id, title, duration_seconds, source_storage_key, source_content_type,
            size_bytes, playlist_storage_key, segment_prefix, packaging_status)
        VALUES (
            '{Guid.NewGuid()}', '{Guid.NewGuid()}', 'Episode', {duration},
            'sources/{Guid.NewGuid():N}', 'audio/wav', {sizeBytes}, {playlist}, {prefix}, '{status}')
        """;

    private async Task RejectAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddRange(parameters);
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

    private async Task<string> QueryStringAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (string)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Query returned no value."));
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
