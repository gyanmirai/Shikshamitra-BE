using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v30 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShiftId",
                table: "AppStandards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShiftId",
                table: "AppEmployees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppInstituteSetupExtraProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppInstituteSetupExtraProperties", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStandards_ShiftId",
                table: "AppStandards",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployees_ShiftId",
                table: "AppEmployees",
                column: "ShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployees_AppShiftManagements_ShiftId",
                table: "AppEmployees",
                column: "ShiftId",
                principalTable: "AppShiftManagements",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppStandards_AppShiftManagements_ShiftId",
                table: "AppStandards",
                column: "ShiftId",
                principalTable: "AppShiftManagements",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployees_AppShiftManagements_ShiftId",
                table: "AppEmployees");

            migrationBuilder.DropForeignKey(
                name: "FK_AppStandards_AppShiftManagements_ShiftId",
                table: "AppStandards");

            migrationBuilder.DropTable(
                name: "AppInstituteSetupExtraProperties");

            migrationBuilder.DropIndex(
                name: "IX_AppStandards_ShiftId",
                table: "AppStandards");

            migrationBuilder.DropIndex(
                name: "IX_AppEmployees_ShiftId",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "AppStandards");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "AppEmployees");
        }
    }
}
