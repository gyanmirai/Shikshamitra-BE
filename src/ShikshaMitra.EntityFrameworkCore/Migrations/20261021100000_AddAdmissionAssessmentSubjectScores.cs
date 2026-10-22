using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    public partial class AddAdmissionAssessmentSubjectScores : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ConfigurationJson",
                table: "AppAdmissionWorkflowStages",
                type: "character varying(64000)",
                maxLength: 64000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8000)",
                oldMaxLength: 8000,
                oldNullable: true);
            migrationBuilder.AddColumn<string>(
                name: "SubjectScoresJson",
                table: "AppAdmissionAssessmentAttempts",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL rejects values exceeding 8000 on rollback; never truncate saved profiles.
            migrationBuilder.AlterColumn<string>(
                name: "ConfigurationJson",
                table: "AppAdmissionWorkflowStages",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64000)",
                oldMaxLength: 64000,
                oldNullable: true);
            migrationBuilder.DropColumn(
                name: "SubjectScoresJson",
                table: "AppAdmissionAssessmentAttempts");
        }
    }
}
