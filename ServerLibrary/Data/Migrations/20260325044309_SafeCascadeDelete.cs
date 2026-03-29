using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerLibrary.Data.Migrations
{
    /// <summary>
    /// Stub migration — the original file was deleted after it was applied to the
    /// database.  This stub exists solely so EF Core can find the class that matches
    /// the row already present in __EFMigrationsHistory, allowing it to continue
    /// applying the migrations that come after it.
    ///
    /// Do NOT remove this file unless you also delete the corresponding row from
    /// __EFMigrationsHistory:
    ///   DELETE FROM [dbo].[__EFMigrationsHistory]
    ///   WHERE MigrationId = '20260325044309_SafeCascadeDelete';
    /// </summary>
    public partial class SafeCascadeDelete : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Already applied — this stub is intentionally empty.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Already applied — this stub is intentionally empty.
        }
    }
}
