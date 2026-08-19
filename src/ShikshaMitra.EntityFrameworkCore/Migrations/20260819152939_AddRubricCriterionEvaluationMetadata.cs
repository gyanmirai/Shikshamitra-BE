using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AddRubricCriterionEvaluationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "AppAssessmentRubricCriteria",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentDescription",
                table: "AppAssessmentRubricCriteria",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherGuidance",
                table: "AppAssessmentRubricCriteria",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "AppAssessmentRubricCriteria");

            migrationBuilder.DropColumn(
                name: "StudentDescription",
                table: "AppAssessmentRubricCriteria");

            migrationBuilder.DropColumn(
                name: "TeacherGuidance",
                table: "AppAssessmentRubricCriteria");
        }
    }
}
