using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shenok.Back.ContentService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillContentCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO page_counters (page_id, subscriber_count)
                SELECT pages.id, 0
                FROM pages
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM page_counters
                    WHERE page_counters.page_id = pages.id);
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO voice_counters (voice_id, play_count)
                SELECT voices.id, 0
                FROM voices
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM voice_counters
                    WHERE voice_counters.voice_id = voices.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
