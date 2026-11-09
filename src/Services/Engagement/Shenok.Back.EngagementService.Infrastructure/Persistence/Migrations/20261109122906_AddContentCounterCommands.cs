using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContentCounterCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "content_counter_commands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    command_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    page_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    play_session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    delta = table.Column<int>(type: "integer", nullable: false),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_counter_commands", x => x.id);
                    table.CheckConstraint("ck_content_counter_commands_kind", "command_kind IN ('Subscriber', 'Play')");
                    table.CheckConstraint("ck_content_counter_commands_shape", "(command_kind = 'Subscriber' AND user_id IS NOT NULL AND page_id IS NOT NULL AND voice_id IS NULL AND play_session_id IS NULL AND delta IN (1, -1))\nOR (command_kind = 'Play' AND user_id IS NULL AND page_id IS NULL AND voice_id IS NOT NULL AND play_session_id IS NOT NULL AND delta = 1)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_content_counter_commands_unpublished",
                table: "content_counter_commands",
                columns: new[] { "occurred_on", "id" },
                filter: "published_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_content_counter_commands_idempotency_key",
                table: "content_counter_commands",
                column: "idempotency_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_counter_commands");
        }
    }
}
