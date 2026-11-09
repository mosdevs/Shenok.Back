using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingVoiceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_voices_pending_id",
                table: "voices",
                column: "id",
                filter: "packaging_status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_voices_pending_id",
                table: "voices");
        }
    }
}
