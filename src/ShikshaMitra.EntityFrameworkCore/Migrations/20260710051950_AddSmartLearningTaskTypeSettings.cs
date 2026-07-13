using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartLearningTaskTypeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultDueDays",
                table: "AppHomeworkTypes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultEvaluationMethod",
                table: "AppHomeworkTypes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultMarks",
                table: "AppHomeworkTypes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultSubmissionType",
                table: "AppHomeworkTypes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AppHomeworkTypes",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EvaluationRequired",
                table: "AppHomeworkTypes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AppHomeworkTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultDueDays",
                table: "AppAssignmentTypes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultEvaluationMethod",
                table: "AppAssignmentTypes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultMarks",
                table: "AppAssignmentTypes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultSubmissionType",
                table: "AppAssignmentTypes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AppAssignmentTypes",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EvaluationRequired",
                table: "AppAssignmentTypes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AppAssignmentTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultDueDays",
                table: "AppHomeworkTypes");

            migrationBuilder.DropColumn(
                name: "DefaultEvaluationMethod",
                table: "AppHomeworkTypes");

            migrationBuilder.DropColumn(
                name: "DefaultMarks",
                table: "AppHomeworkTypes");

            migrationBuilder.DropColumn(
                name: "DefaultSubmissionType",
                table: "AppHomeworkTypes");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AppHomeworkTypes");

            migrationBuilder.DropColumn(
                name: "EvaluationRequired",
                table: "AppHomeworkTypes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppHomeworkTypes");

            migrationBuilder.DropColumn(
                name: "DefaultDueDays",
                table: "AppAssignmentTypes");

            migrationBuilder.DropColumn(
                name: "DefaultEvaluationMethod",
                table: "AppAssignmentTypes");

            migrationBuilder.DropColumn(
                name: "DefaultMarks",
                table: "AppAssignmentTypes");

            migrationBuilder.DropColumn(
                name: "DefaultSubmissionType",
                table: "AppAssignmentTypes");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "AppAssignmentTypes");

            migrationBuilder.DropColumn(
                name: "EvaluationRequired",
                table: "AppAssignmentTypes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AppAssignmentTypes");
        }
    }
}
