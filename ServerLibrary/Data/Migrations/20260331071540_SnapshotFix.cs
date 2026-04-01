using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Employees_EmployeeId",
                table: "Feedbacks");

            migrationBuilder.DropIndex(
                name: "IX_Employees_CivilId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees");

            migrationBuilder.AlterColumn<string>(
                name: "FileNumber",
                table: "Employees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CivilId",
                table: "Employees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "ApplicationUsers",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    NoteText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentimentScore = table.Column<float>(type: "real", nullable: false),
                    SentimentLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeNotes_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CivilId",
                table: "Employees",
                column: "CivilId",
                unique: true,
                filter: "[CivilId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees",
                column: "FileNumber",
                unique: true,
                filter: "[FileNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_Email",
                table: "ApplicationUsers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeNotes_CreatedAt",
                table: "EmployeeNotes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeNotes_EmployeeId",
                table: "EmployeeNotes",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Employees_EmployeeId",
                table: "Feedbacks",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Employees_EmployeeId",
                table: "Feedbacks");

            migrationBuilder.DropTable(
                name: "EmployeeNotes");

            migrationBuilder.DropIndex(
                name: "IX_Employees_CivilId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_Email",
                table: "ApplicationUsers");

            migrationBuilder.AlterColumn<string>(
                name: "FileNumber",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CivilId",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "ApplicationUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CivilId",
                table: "Employees",
                column: "CivilId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees",
                column: "FileNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Employees_EmployeeId",
                table: "Feedbacks",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id");
        }
    }
}
