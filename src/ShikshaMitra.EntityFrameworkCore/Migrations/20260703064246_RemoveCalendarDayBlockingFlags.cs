using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCalendarDayBlockingFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlocksExamSchedule",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "BlocksHomeworkDueDate",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "BlocksLessonPlan",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "BlocksOnlineClass",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "BlocksTimetable",
                table: "AppAcademicCalendarDays");

            migrationBuilder.DropColumn(
                name: "ExemptAttendance",
                table: "AppAcademicCalendarDays");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BlocksExamSchedule",
                table: "AppAcademicCalendarDays",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BlocksHomeworkDueDate",
                table: "AppAcademicCalendarDays",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BlocksLessonPlan",
                table: "AppAcademicCalendarDays",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BlocksOnlineClass",
                table: "AppAcademicCalendarDays",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "BlocksTimetable",
                table: "AppAcademicCalendarDays",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExemptAttendance",
                table: "AppAcademicCalendarDays",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
