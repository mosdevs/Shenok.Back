using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialEngagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "listening_records",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    last_played_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listening_records", x => new { x.user_id, x.voice_id });
                });

            migrationBuilder.CreateTable(
                name: "play_sessions",
                columns: table => new
                {
                    play_session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    counted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_play_sessions", x => x.play_session_id);
                });

            migrationBuilder.CreateTable(
                name: "scan_receipts",
                columns: table => new
                {
                    scan_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waveform_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    unlocked = table.Column<bool>(type: "boolean", nullable: false),
                    is_following = table.Column<bool>(type: "boolean", nullable: false),
                    prompt_follow = table.Column<bool>(type: "boolean", nullable: false),
                    resume_position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scan_receipts", x => x.scan_id);
                });

            migrationBuilder.CreateTable(
                name: "user_page_relationships",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_following = table.Column<bool>(type: "boolean", nullable: false),
                    followed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    unlocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_page_relationships", x => new { x.user_id, x.page_id });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listening_records");

            migrationBuilder.DropTable(
                name: "play_sessions");

            migrationBuilder.DropTable(
                name: "scan_receipts");

            migrationBuilder.DropTable(
                name: "user_page_relationships");
        }
    }
}
