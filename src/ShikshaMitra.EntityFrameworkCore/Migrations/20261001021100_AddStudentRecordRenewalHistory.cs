using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <summary>Keep closed reviews while enforcing one active renewal per student and year.</summary>
    public partial class AddStudentRecordRenewalHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppStudentRecordRenewalCases_TenantId_StudentId_AcademicYea~",
                table: "AppStudentRecordRenewalCases");
            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordRenewalCases_TenantId_StudentId_AcademicYea~",
                table: "AppStudentRecordRenewalCases",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" NOT IN ('Completed','Cancelled')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $records_renewal_rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AppStudentRecordRenewalCases" WHERE "IsDeleted" = false
                        GROUP BY "TenantId", "StudentId", "AcademicYearId" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Student Records rollback cannot retain repeated renewal history under the previous index; use a forward fix.';
                    END IF;
                END $records_renewal_rollback$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_AppStudentRecordRenewalCases_TenantId_StudentId_AcademicYea~",
                table: "AppStudentRecordRenewalCases");
            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordRenewalCases_TenantId_StudentId_AcademicYea~",
                table: "AppStudentRecordRenewalCases",
                columns: new[] { "TenantId", "StudentId", "AcademicYearId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
