using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormNo",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "Floor",
                table: "AppRooms");

            migrationBuilder.AddColumn<Guid>(
                name: "FloorId",
                table: "AppRooms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FloorId",
                table: "AppRooms");

            migrationBuilder.AddColumn<string>(
                name: "FormNo",
                table: "AppStudentAdditionalDetails",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Floor",
                table: "AppRooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
