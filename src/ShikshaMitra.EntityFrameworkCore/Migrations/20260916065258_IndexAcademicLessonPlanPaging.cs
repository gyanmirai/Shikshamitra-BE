using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class IndexAcademicLessonPlanPaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_academic_LessonPlans_TenantId_AcademicYearId_CreationTime",
                table: "academic_LessonPlans",
                columns: new[] { "TenantId", "AcademicYearId", "CreationTime" },
                descending: new[] { false, false, true },
                filter: "NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_academic_LessonPlans_TenantId_AcademicYearId_CreationTime",
                table: "academic_LessonPlans");
        }
    }
}
