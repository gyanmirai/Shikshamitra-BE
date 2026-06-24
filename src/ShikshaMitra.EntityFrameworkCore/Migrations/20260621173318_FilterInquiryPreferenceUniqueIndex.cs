using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class FilterInquiryPreferenceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppInquiryPreferences_TenantId_AcademicYearId_PreferenceName",
                table: "AppInquiryPreferences");

            migrationBuilder.CreateIndex(
                name: "IX_AppInquiryPreferences_TenantId_AcademicYearId_PreferenceName",
                table: "AppInquiryPreferences",
                columns: new[] { "TenantId", "AcademicYearId", "PreferenceName" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppInquiryPreferences_TenantId_AcademicYearId_PreferenceName",
                table: "AppInquiryPreferences");

            migrationBuilder.CreateIndex(
                name: "IX_AppInquiryPreferences_TenantId_AcademicYearId_PreferenceName",
                table: "AppInquiryPreferences",
                columns: new[] { "TenantId", "AcademicYearId", "PreferenceName" },
                unique: true);
        }
    }
}
