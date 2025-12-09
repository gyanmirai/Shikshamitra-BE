using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppInquiryPreferences_TenantId_PreferenceName",
                table: "AppInquiryPreferences");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppInquiryPreferences",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AppInquiryPreferences_TenantId_AcademicYearId_PreferenceName",
                table: "AppInquiryPreferences",
                columns: new[] { "TenantId", "AcademicYearId", "PreferenceName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppInquiryPreferences_TenantId_AcademicYearId_PreferenceName",
                table: "AppInquiryPreferences");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppInquiryPreferences");

            migrationBuilder.CreateIndex(
                name: "IX_AppInquiryPreferences_TenantId_PreferenceName",
                table: "AppInquiryPreferences",
                columns: new[] { "TenantId", "PreferenceName" },
                unique: true);
        }
    }
}
