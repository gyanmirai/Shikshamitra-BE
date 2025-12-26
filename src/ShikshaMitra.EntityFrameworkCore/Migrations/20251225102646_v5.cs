using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HouseId",
                table: "AppStudentAdditionalDetails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDivisionHouseLeader",
                table: "AppStudentAdditionalDetails",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGlobalHouseLeader",
                table: "AppStudentAdditionalDetails",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAdditionalDetails_HouseId",
                table: "AppStudentAdditionalDetails",
                column: "HouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStudentAdditionalDetails_AppHouseSettings_HouseId",
                table: "AppStudentAdditionalDetails",
                column: "HouseId",
                principalTable: "AppHouseSettings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppStudentAdditionalDetails_AppHouseSettings_HouseId",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentAdditionalDetails_HouseId",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "HouseId",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "IsDivisionHouseLeader",
                table: "AppStudentAdditionalDetails");

            migrationBuilder.DropColumn(
                name: "IsGlobalHouseLeader",
                table: "AppStudentAdditionalDetails");
        }
    }
}
