using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VoiceRequiresPage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_voices_pages_page_id",
                table: "voices",
                column: "page_id",
                principalTable: "pages",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_voices_pages_page_id",
                table: "voices");
        }
    }
}
