using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexesAndFixAuditLogEmployeeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. AuditLogs.EmployeeId: nvarchar(max) → int (nullable) ──────────
            // The AuditEvent DTO and all publishers use int for EmployeeId.
            // The previous nvarchar column caused System.Text.Json to throw a
            // JsonException when the consumer tried to deserialise the number 123
            // into a string field, causing messages to be permanently discarded.
            migrationBuilder.AlterColumn<int>(
                name: "EmployeeId",
                table: "AuditLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // ── 2. Unique index on Employees.CivilId ─────────────────────────────
            // nvarchar(max) cannot be a key column in SQL Server. Shrink to 100
            // first, then deduplicate any existing rows, then create the unique index.
            migrationBuilder.AlterColumn<string>(
                name: "CivilId",
                table: "Employees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // Fix duplicate CivilId values by appending -2, -3, etc. to later rows
            migrationBuilder.Sql(@"
                WITH cte AS (
                    SELECT Id, CivilId,
                           ROW_NUMBER() OVER (PARTITION BY CivilId ORDER BY Id) AS rn
                    FROM Employees
                    WHERE CivilId IS NOT NULL
                )
                UPDATE cte
                SET CivilId = CivilId + '-DUP' + CAST(rn AS nvarchar(10))
                WHERE rn > 1;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CivilId",
                table: "Employees",
                column: "CivilId",
                unique: true,
                filter: "[CivilId] IS NOT NULL");

            // ── 3. Unique index on Employees.FileNumber ───────────────────────────
            // Same reason — shrink from nvarchar(max) before indexing.
            migrationBuilder.AlterColumn<string>(
                name: "FileNumber",
                table: "Employees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // Fix duplicate FileNumber values by appending -2, -3, etc. to later rows
            migrationBuilder.Sql(@"
                WITH cte AS (
                    SELECT Id, FileNumber,
                           ROW_NUMBER() OVER (PARTITION BY FileNumber ORDER BY Id) AS rn
                    FROM Employees
                    WHERE FileNumber IS NOT NULL
                )
                UPDATE cte
                SET FileNumber = FileNumber + '-DUP' + CAST(rn AS nvarchar(10))
                WHERE rn > 1;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees",
                column: "FileNumber",
                unique: true,
                filter: "[FileNumber] IS NOT NULL");

            // ── Note on cascade delete ────────────────────────────────────────────
            // Employee → Vacation, Overtime, Sanction, Doctor cascade delete was
            // already present in the database (EF Core conventions inferred it in
            // earlier migrations).  AppDbContext.OnModelCreating now makes it
            // explicit with named fluent config, but no DDL change is required here.

            // ── Note on TownId / BranchId indexes ────────────────────────────────
            // IX_Employees_BranchId and IX_Employees_TownId already exist from
            // earlier migrations — no action needed.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_CivilId",
                table: "Employees");

            migrationBuilder.AlterColumn<string>(
                name: "CivilId",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees");

            migrationBuilder.AlterColumn<string>(
                name: "FileNumber",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmployeeId",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
