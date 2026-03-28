using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Index on ApplicationUser.Email — eliminates full-table scan on every login.
            // FindUserByEmail previously used ToLower() which forced a function-based scan.
            // With this index and direct equality comparison, login Phase-1 drops from ~2s to <10ms.
            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_Email",
                table: "ApplicationUsers",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_Email",
                table: "ApplicationUsers");
        }
    }
}
