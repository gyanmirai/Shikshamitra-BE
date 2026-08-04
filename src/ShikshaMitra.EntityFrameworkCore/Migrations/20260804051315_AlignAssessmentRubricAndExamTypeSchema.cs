using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class AlignAssessmentRubricAndExamTypeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowPracticalOral",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "GradeScaleRequired",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "IncludedInResult",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "MarksEntryRequired",
                table: "AppExamTypes");

            migrationBuilder.DropColumn(
                name: "EvaluationInstructions",
                table: "AppAcademicCalendars");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicMediumId",
                table: "AppAssessmentRubrics",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppliesToWorkType",
                table: "AppAssessmentRubrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentTypeId",
                table: "AppAssessmentRubrics",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationInstructions",
                table: "AppAssessmentRubrics",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HomeworkTypeId",
                table: "AppAssessmentRubrics",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "AppAssessmentRubrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceTemplateId",
                table: "AppAssessmentRubrics",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAssessmentRubrics_AcademicMediumId",
                table: "AppAssessmentRubrics",
                column: "AcademicMediumId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssessmentRubrics_AssignmentTypeId",
                table: "AppAssessmentRubrics",
                column: "AssignmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssessmentRubrics_HomeworkTypeId",
                table: "AppAssessmentRubrics",
                column: "HomeworkTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssessmentRubrics_IsTemplate",
                table: "AppAssessmentRubrics",
                column: "IsTemplate");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssessmentRubrics_SourceTemplateId",
                table: "AppAssessmentRubrics",
                column: "SourceTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAssessmentRubrics_AcademicMediumId",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropIndex(
                name: "IX_AppAssessmentRubrics_AssignmentTypeId",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropIndex(
                name: "IX_AppAssessmentRubrics_HomeworkTypeId",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropIndex(
                name: "IX_AppAssessmentRubrics_IsTemplate",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropIndex(
                name: "IX_AppAssessmentRubrics_SourceTemplateId",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropColumn(
                name: "AcademicMediumId",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropColumn(
                name: "AppliesToWorkType",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropColumn(
                name: "AssignmentTypeId",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropColumn(
                name: "EvaluationInstructions",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropColumn(
                name: "HomeworkTypeId",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropColumn(
                name: "SourceTemplateId",
                table: "AppAssessmentRubrics");

            migrationBuilder.AddColumn<bool>(
                name: "AllowPracticalOral",
                table: "AppExamTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "GradeScaleRequired",
                table: "AppExamTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludedInResult",
                table: "AppExamTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MarksEntryRequired",
                table: "AppExamTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationInstructions",
                table: "AppAcademicCalendars",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }
    }
}
