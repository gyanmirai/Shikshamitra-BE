using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentRecordTransitionTargetScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TargetAcademicYearId",
                table: "AppStudentRecordTransitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetDivisionId",
                table: "AppStudentRecordTransitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetStandardId",
                table: "AppStudentRecordTransitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentRecordTransitions_TenantId_TargetAcademicYearId_T~",
                table: "AppStudentRecordTransitions",
                columns: new[] { "TenantId", "TargetAcademicYearId", "TargetStandardId", "TargetDivisionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $records_scope_rollback$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AppStudentRecordTransitions"
                        WHERE "TargetAcademicYearId" IS NOT NULL OR "TargetStandardId" IS NOT NULL
                           OR "TargetDivisionId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Student Records rollback would remove target scope evidence; use a forward fix.';
                    END IF;
                END $records_scope_rollback$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_AppStudentRecordTransitions_TenantId_TargetAcademicYearId_T~",
                table: "AppStudentRecordTransitions");

            migrationBuilder.DropColumn(
                name: "TargetAcademicYearId",
                table: "AppStudentRecordTransitions");

            migrationBuilder.DropColumn(
                name: "TargetDivisionId",
                table: "AppStudentRecordTransitions");

            migrationBuilder.DropColumn(
                name: "TargetStandardId",
                table: "AppStudentRecordTransitions");
        }
    }
}
