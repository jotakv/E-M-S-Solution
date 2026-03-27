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
            // CivilId is a unique employee identifier (format: CIV-###).
            // IMPORTANT: run the cleanup script below to deduplicate any existing
            // rows before applying this migration to a populated database.
            migrationBuilder.CreateIndex(
                name: "IX_Employees_CivilId",
                table: "Employees",
                column: "CivilId",
                unique: true);

            // ── 3. Unique index on Employees.FileNumber ───────────────────────────
            // FileNumber is a unique employee identifier (format: EMP-###).
            // IMPORTANT: run the cleanup script below before migrating a populated DB.
            migrationBuilder.CreateIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees",
                column: "FileNumber",
                unique: true);

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

            migrationBuilder.DropIndex(
                name: "IX_Employees_FileNumber",
                table: "Employees");

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
