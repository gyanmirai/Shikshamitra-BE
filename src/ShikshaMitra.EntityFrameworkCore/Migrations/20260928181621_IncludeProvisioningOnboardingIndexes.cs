using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class IncludeProvisioningOnboardingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_AcademicYearId_DivisionI~",
                table: "AppStudentOnboardingCases");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_StudentId_AcademicYearId",
                table: "AppStudentOnboardingCases");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_AcademicYearId_DivisionI~",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "AcademicYearId", "DivisionId", "RollNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"DivisionId\" IS NOT NULL AND \"RollNumber\" IS NOT NULL AND \"Status\" IN (0, 1, 2, 3, 6)");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_StudentId_AcademicYearId",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN (0, 1, 2, 3, 6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AppStudentOnboardingServiceHandoffs" WHERE "ServiceKey" = 'finance-first-payment' AND NOT "IsDeleted") THEN
                        RAISE EXCEPTION 'Onboarding provisioning records exist. Use a forward fix instead of rolling back to a runtime that cannot interpret them.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_AcademicYearId_DivisionI~",
                table: "AppStudentOnboardingCases");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_StudentId_AcademicYearId",
                table: "AppStudentOnboardingCases");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_AcademicYearId_DivisionI~",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "AcademicYearId", "DivisionId", "RollNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"DivisionId\" IS NOT NULL AND \"RollNumber\" IS NOT NULL AND \"Status\" IN (0, 1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentOnboardingCases_TenantId_StudentId_AcademicYearId",
                table: "AppStudentOnboardingCases",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN (0, 1, 2, 3)");
        }
    }
}
