using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeNoteTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeeNotes",
                columns: table => new
                {
                    Id              = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId      = table.Column<int>(type: "int",           nullable: false),
                    NoteText        = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentimentScore  = table.Column<float>(type: "real",        nullable: false),
                    SentimentLabel  = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt       = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeNotes", x => x.Id);
                    table.ForeignKey(
                        name:           "FK_EmployeeNotes_Employees_EmployeeId",
                        column:         x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete:       ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name:   "IX_EmployeeNotes_EmployeeId",
                table:  "EmployeeNotes",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name:   "IX_EmployeeNotes_CreatedAt",
                table:  "EmployeeNotes",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EmployeeNotes");
        }
    }
}
