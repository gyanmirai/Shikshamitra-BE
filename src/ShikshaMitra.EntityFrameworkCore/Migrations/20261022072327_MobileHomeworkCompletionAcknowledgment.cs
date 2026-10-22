using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class MobileHomeworkCompletionAcknowledgment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Acknowledged",
                table: "academic_StudentHomeworkAssignmentSubmissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Acknowledged",
                table: "academic_MobileSubmissionSnapshots",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Acknowledged",
                table: "academic_StudentHomeworkAssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "Acknowledged",
                table: "academic_MobileSubmissionSnapshots");
        }
    }
}
