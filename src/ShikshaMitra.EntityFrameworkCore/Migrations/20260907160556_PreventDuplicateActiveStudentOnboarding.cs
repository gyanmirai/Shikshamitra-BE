using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PreventDuplicateActiveStudentOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_StudentId_AcademicYearId",
                table: "AppStudentOnboardingCases");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_StudentId_AcademicYearId",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN (0, 1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_StudentId_AcademicYearId",
                table: "AppStudentOnboardingCases");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_StudentId_AcademicYearId",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" });
        }
    }
}
