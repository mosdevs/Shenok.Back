using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shenok.Back.RecognitionService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialRecognition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    received_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.event_id);
                    table.CheckConstraint("ck_inbox_messages_type", "message_type IN ('WaveformRegistered', 'WaveformRevoked')");
                });

            migrationBuilder.CreateTable(
                name: "waveform_index",
                columns: table => new
                {
                    waveform_code = table.Column<long>(type: "bigint", nullable: false),
                    waveform_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    layout_version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    source_occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waveform_index", x => x.waveform_code);
                    table.CheckConstraint("ck_waveform_index_code", "waveform_code > 0 AND waveform_code <= 1099511627775");
                    table.CheckConstraint("ck_waveform_index_status", "status IN ('Active', 'Revoked')");
                    table.CheckConstraint("ck_waveform_index_target", "target_type IN ('Page', 'Voice')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "waveform_index");
        }
    }
}
