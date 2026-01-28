using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicYearIdToEventAndHoliday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppHolidays",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppEvents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "AppHolidays_AcademicYearId_Tenant",
                table: "AppHolidays",
                columns: new[] { "AcademicYearId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppEvents_AcademicYearId_TenantId",
                table: "AppEvents",
                columns: new[] { "AcademicYearId", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "AppHolidays_AcademicYearId_Tenant",
                table: "AppHolidays");

            migrationBuilder.DropIndex(
                name: "IX_AppEvents_AcademicYearId_TenantId",
                table: "AppEvents");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppHolidays");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppEvents");
        }
    }
}
