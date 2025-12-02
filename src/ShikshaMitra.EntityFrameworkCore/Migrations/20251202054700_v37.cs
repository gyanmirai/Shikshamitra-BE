using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class v37 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppHomeworkAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EducationMediumStageId",
                table: "AppEmployeeAttendances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicYearId",
                table: "AppClassTeachers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEmployeeAttendances_EducationMediumStageId",
                table: "AppEmployeeAttendances",
                column: "EducationMediumStageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppEmployeeAttendances_EducationMediumStageId",
                table: "AppEmployeeAttendances");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppHomeworkAssignments");

            migrationBuilder.DropColumn(
                name: "EducationMediumStageId",
                table: "AppEmployeeAttendances");

            migrationBuilder.DropColumn(
                name: "AcademicYearId",
                table: "AppClassTeachers");
        }
    }
}
