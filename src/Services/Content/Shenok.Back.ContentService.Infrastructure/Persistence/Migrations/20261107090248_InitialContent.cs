using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "waveform_code_seq",
                minValue: 1L,
                maxValue: 1099511627775L);

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                    table.CheckConstraint("ck_outbox_messages_type", "message_type IN ('WaveformRegistered', 'WaveformRevoked')");
                });

            migrationBuilder.CreateTable(
                name: "page_counters",
                columns: table => new
                {
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_page_counters", x => x.page_id);
                    table.CheckConstraint("ck_page_counters_subscriber_non_negative", "subscriber_count >= 0");
                });

            migrationBuilder.CreateTable(
                name: "pages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    cover_image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    voice_ids = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pages", x => x.id);
                    table.CheckConstraint("ck_pages_status", "status IN ('Draft', 'Published', 'Archived')");
                    table.CheckConstraint("ck_pages_voice_ids_array", "jsonb_typeof(voice_ids) = 'array'");
                });

            migrationBuilder.CreateTable(
                name: "voice_counters",
                columns: table => new
                {
                    voice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    play_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voice_counters", x => x.voice_id);
                    table.CheckConstraint("ck_voice_counters_play_count_non_negative", "play_count >= 0");
                });

            migrationBuilder.CreateTable(
                name: "voices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: true),
                    source_storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    source_content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    playlist_storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    segment_prefix = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    packaging_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voices", x => x.id);
                    table.CheckConstraint("ck_voices_packaging", "(\n    packaging_status = 'Pending'\n    AND duration_seconds IS NULL\n    AND playlist_storage_key IS NULL\n    AND segment_prefix IS NULL\n)\nOR\n(\n    packaging_status = 'Failed'\n    AND duration_seconds IS NULL\n    AND playlist_storage_key IS NULL\n    AND segment_prefix IS NULL\n)\nOR\n(\n    packaging_status = 'Ready'\n    AND duration_seconds > 0\n    AND duration_seconds <= 3600\n    AND playlist_storage_key IS NOT NULL\n    AND segment_prefix IS NOT NULL\n)");
                    table.CheckConstraint("ck_voices_size_bytes", "size_bytes > 0 AND size_bytes <= 104857600");
                });

            migrationBuilder.CreateTable(
                name: "waveform_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    waveform_code = table.Column<long>(type: "bigint", nullable: false),
                    target_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    layout_version = table.Column<int>(type: "integer", nullable: false),
                    registered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waveform_mappings", x => x.id);
                    table.CheckConstraint("ck_waveform_mappings_code_range", "waveform_code > 0 AND waveform_code <= 1099511627775");
                    table.CheckConstraint("ck_waveform_mappings_layout_version", "layout_version = 1");
                    table.CheckConstraint("ck_waveform_mappings_status", "status IN ('Active', 'Revoked')");
                    table.CheckConstraint("ck_waveform_mappings_target_type", "target_type IN ('Page', 'Voice')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unpublished",
                table: "outbox_messages",
                column: "published_on",
                filter: "published_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_pages_owner_id",
                table: "pages",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_voices_packaging_status",
                table: "voices",
                column: "packaging_status");

            migrationBuilder.CreateIndex(
                name: "ix_voices_page_id",
                table: "voices",
                column: "page_id");

            migrationBuilder.CreateIndex(
                name: "ix_voices_playlist_storage_key",
                table: "voices",
                column: "playlist_storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_voices_segment_prefix",
                table: "voices",
                column: "segment_prefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_voices_source_storage_key",
                table: "voices",
                column: "source_storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_waveform_mappings_target",
                table: "waveform_mappings",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_waveform_mappings_waveform_code",
                table: "waveform_mappings",
                column: "waveform_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "page_counters");

            migrationBuilder.DropTable(
                name: "pages");

            migrationBuilder.DropTable(
                name: "voice_counters");

            migrationBuilder.DropTable(
                name: "voices");

            migrationBuilder.DropTable(
                name: "waveform_mappings");

            migrationBuilder.DropSequence(
                name: "waveform_code_seq");
        }
    }
}
