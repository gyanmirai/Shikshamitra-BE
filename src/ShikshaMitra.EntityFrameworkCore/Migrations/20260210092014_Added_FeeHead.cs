using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class Added_FeeHead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FeeHeadName",
                table: "FeeHeads",
                newName: "Name");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "FeeHeads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "FeeHeads",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "FeeCategoryId",
                table: "FeeHeads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "FeeHeads",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "FeeHeads");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "FeeHeads");

            migrationBuilder.DropColumn(
                name: "FeeCategoryId",
                table: "FeeHeads");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "FeeHeads");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "FeeHeads",
                newName: "FeeHeadName");
        }
    }
}
