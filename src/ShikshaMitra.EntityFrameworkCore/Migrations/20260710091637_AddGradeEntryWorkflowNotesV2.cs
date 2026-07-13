using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeEntryWorkflowNotesV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewerNote",
                table: "AppExamMarksWorkflowStates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnlockReason",
                table: "AppExamMarksWorkflowStates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewerNote",
                table: "AppExamMarksWorkflowStates");

            migrationBuilder.DropColumn(
                name: "UnlockReason",
                table: "AppExamMarksWorkflowStates");
        }
    }
}
