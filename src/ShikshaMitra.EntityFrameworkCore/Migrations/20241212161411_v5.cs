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
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinCode",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "AppEmployees",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployees_RoleId",
                table: "AppEmployees",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployees_AbpRoles_RoleId",
                table: "AppEmployees",
                column: "RoleId",
                principalTable: "AbpRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployees_AbpRoles_RoleId",
                table: "AppEmployees");

            migrationBuilder.DropIndex(
                name: "IX_AppEmployees_RoleId",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "City",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "PinCode",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "State",
                table: "AppEmployees");
        }
    }
}
