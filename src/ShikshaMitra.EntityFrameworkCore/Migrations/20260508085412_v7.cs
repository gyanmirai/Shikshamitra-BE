using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedOn",
                table: "StudentFeeStructures");

            migrationBuilder.DropColumn(
                name: "StandardId",
                table: "StudentFeeStructures");

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "FeeStructures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "FeeStructureId",
                table: "AppStudents",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "FeeStructures");

            migrationBuilder.DropColumn(
                name: "FeeStructureId",
                table: "AppStudents");

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedOn",
                table: "StudentFeeStructures",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "StandardId",
                table: "StudentFeeStructures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
