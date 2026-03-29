using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Feedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId      = table.Column<int>(type: "int",              nullable: true),
                    Comment         = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SentimentScore  = table.Column<float>(type: "real",            nullable: false),
                    IsPositive      = table.Column<bool>(type: "bit",              nullable: false),
                    CreatedAt       = table.Column<DateTime>(type: "datetime2",    nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedbacks", x => x.Id);
                    table.ForeignKey(
                        name:       "FK_Feedbacks_Employees_EmployeeId",
                        column:     x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete:   ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name:    "IX_Feedbacks_EmployeeId",
                table:   "Feedbacks",
                column:  "EmployeeId");

            migrationBuilder.CreateIndex(
                name:    "IX_Feedbacks_CreatedAt",
                table:   "Feedbacks",
                column:  "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Feedbacks");
        }
    }
}
