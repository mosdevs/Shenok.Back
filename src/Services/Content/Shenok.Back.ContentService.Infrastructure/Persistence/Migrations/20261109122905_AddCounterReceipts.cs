using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCounterReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "play_counter_receipts",
                columns: table => new
                {
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    voice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_play_counter_receipts", x => x.idempotency_key);
                });

            migrationBuilder.CreateTable(
                name: "subscriber_counter_receipts",
                columns: table => new
                {
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delta = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriber_counter_receipts", x => x.idempotency_key);
                    table.CheckConstraint("ck_subscriber_counter_receipts_delta", "delta IN (1, -1)");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "play_counter_receipts");

            migrationBuilder.DropTable(
                name: "subscriber_counter_receipts");
        }
    }
}
