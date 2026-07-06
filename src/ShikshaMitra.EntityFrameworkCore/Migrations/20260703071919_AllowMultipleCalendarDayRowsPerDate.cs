using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleCalendarDayRowsPerDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAcademicCalendarDays_AcademicCalendarId_Date",
                table: "AppAcademicCalendarDays");

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicCalendarDays_AcademicCalendarId_Date",
                table: "AppAcademicCalendarDays",
                columns: new[] { "AcademicCalendarId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAcademicCalendarDays_AcademicCalendarId_Date",
                table: "AppAcademicCalendarDays");

            migrationBuilder.CreateIndex(
                name: "IX_AppAcademicCalendarDays_AcademicCalendarId_Date",
                table: "AppAcademicCalendarDays",
                columns: new[] { "AcademicCalendarId", "Date" },
                unique: true);
        }
    }
}
