using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class RestrictTownEmployeeCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old CASCADE FK from Employees → Towns
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Towns_TownId",
                table: "Employees");

            // Re-add it with RESTRICT so deleting a Town is blocked when employees are assigned
            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Towns_TownId",
                table: "Employees",
                column: "TownId",
                principalTable: "Towns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Towns_TownId",
                table: "Employees");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Towns_TownId",
                table: "Employees",
                column: "TownId",
                principalTable: "Towns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
