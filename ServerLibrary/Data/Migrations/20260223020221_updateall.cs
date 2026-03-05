using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerLibrary.Data.Migrations
{
    /// <inheritdoc />
    public partial class updateall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sanctions_SanctionTypes_SanctionTypeId",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "Civilld",
                table: "Vacations");

            migrationBuilder.DropColumn(
                name: "FileNumber",
                table: "Vacations");

            migrationBuilder.DropColumn(
                name: "Other",
                table: "Vacations");

            migrationBuilder.DropColumn(
                name: "Civilld",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "FileNumber",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "Other",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "Civilld",
                table: "Overtimes");

            migrationBuilder.DropColumn(
                name: "FileNumber",
                table: "Overtimes");

            migrationBuilder.DropColumn(
                name: "Other",
                table: "Overtimes");

            migrationBuilder.DropColumn(
                name: "Civilld",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "FileNumber",
                table: "Doctors");

            migrationBuilder.DropColumn(
                name: "Other",
                table: "Doctors");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "Vacations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "SanctionTypeId",
                table: "Sanctions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "Sanctions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "Overtimes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EmployeeId",
                table: "Doctors",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_Sanctions_SanctionTypes_SanctionTypeId",
                table: "Sanctions",
                column: "SanctionTypeId",
                principalTable: "SanctionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sanctions_SanctionTypes_SanctionTypeId",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Vacations");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Sanctions");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Overtimes");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Doctors");

            migrationBuilder.AddColumn<string>(
                name: "Civilld",
                table: "Vacations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileNumber",
                table: "Vacations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Other",
                table: "Vacations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SanctionTypeId",
                table: "Sanctions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Civilld",
                table: "Sanctions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileNumber",
                table: "Sanctions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Other",
                table: "Sanctions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Civilld",
                table: "Overtimes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileNumber",
                table: "Overtimes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Other",
                table: "Overtimes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Civilld",
                table: "Doctors",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FileNumber",
                table: "Doctors",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Other",
                table: "Doctors",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sanctions_SanctionTypes_SanctionTypeId",
                table: "Sanctions",
                column: "SanctionTypeId",
                principalTable: "SanctionTypes",
                principalColumn: "Id");
        }
    }
}
