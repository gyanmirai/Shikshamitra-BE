using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministratorReportingManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReportingManagerUserId",
                table: "AppEmployees",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployees_ReportingManagerUserId",
                table: "AppEmployees",
                column: "ReportingManagerUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Employees_ReportingManagerIdentity",
                table: "AppEmployees",
                sql: "\"ReportingManagerId\" IS NULL OR \"ReportingManagerUserId\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AppEmployees_AbpUsers_ReportingManagerUserId",
                table: "AppEmployees",
                column: "ReportingManagerUserId",
                principalTable: "AbpUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppEmployees_AbpUsers_ReportingManagerUserId",
                table: "AppEmployees");

            migrationBuilder.DropIndex(
                name: "IX_AppEmployees_ReportingManagerUserId",
                table: "AppEmployees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Employees_ReportingManagerIdentity",
                table: "AppEmployees");

            migrationBuilder.DropColumn(
                name: "ReportingManagerUserId",
                table: "AppEmployees");
        }
    }
}
