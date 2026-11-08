using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shenok.Back.EngagementService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlaybackPositionNonNegative : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_scan_receipts_resume_position_non_negative",
                table: "scan_receipts",
                sql: "resume_position >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_listening_records_position_non_negative",
                table: "listening_records",
                sql: "position >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_scan_receipts_resume_position_non_negative",
                table: "scan_receipts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_listening_records_position_non_negative",
                table: "listening_records");
        }
    }
}
