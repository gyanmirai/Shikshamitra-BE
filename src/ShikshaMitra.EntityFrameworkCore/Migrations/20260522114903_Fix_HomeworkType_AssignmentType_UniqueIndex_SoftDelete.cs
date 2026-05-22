using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class Fix_HomeworkType_AssignmentType_UniqueIndex_SoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HomeworkTypes_AcademicMediumId_Name",
                table: "AppHomeworkTypes");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentTypes_AcademicMediumId_Name",
                table: "AppAssignmentTypes");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkTypes_AcademicMediumId_Name",
                table: "AppHomeworkTypes",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentTypes_AcademicMediumId_Name",
                table: "AppAssignmentTypes",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HomeworkTypes_AcademicMediumId_Name",
                table: "AppHomeworkTypes");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentTypes_AcademicMediumId_Name",
                table: "AppAssignmentTypes");

            migrationBuilder.CreateIndex(
                name: "IX_HomeworkTypes_AcademicMediumId_Name",
                table: "AppHomeworkTypes",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentTypes_AcademicMediumId_Name",
                table: "AppAssignmentTypes",
                columns: new[] { "AcademicMediumId", "Name" },
                unique: true);
        }
    }
}
