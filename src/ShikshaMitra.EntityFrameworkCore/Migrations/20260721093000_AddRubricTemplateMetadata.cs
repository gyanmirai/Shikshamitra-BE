using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ShikshaMitraDbContext))]
    [Migration("20260721093000_AddRubricTemplateMetadata")]
    public partial class AddRubricTemplateMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "HomeworkTypeId",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "AppAssessmentRubrics");

            migrationBuilder.DropColumn(
                name: "SourceTemplateId",
                table: "AppAssessmentRubrics");
        }
    }
}
