using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class Upgraded_To_Abp_8_3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "Photo",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "AppEmployees");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "AppEmployees",
                newName: "EmployeeCode");

            migrationBuilder.RenameColumn(
                name: "Gender",
                table: "AppEmployees",
                newName: "GenderType");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeEmployeeStatusTypeType",
                table: "AppEmployees",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "AppEmployees",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeEmployeeStatusTypeType",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "AppEmployees");

            migrationBuilder.RenameColumn(
                name: "GenderType",
                table: "AppEmployees",
                newName: "Gender");

            migrationBuilder.RenameColumn(
                name: "EmployeeCode",
                table: "AppEmployees",
                newName: "Status");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Photo",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AppEmployees",
                type: "text",
                nullable: true);
        }
    }
}
