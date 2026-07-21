using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260721111500_AddRubricBuilderMetadata")]
public partial class AddRubricBuilderMetadata : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(
			name: "EvaluationInstructions",
			table: "AppAssessmentRubrics",
			type: "character varying(2048)",
			maxLength: 2048,
			nullable: true);

		migrationBuilder.AddColumn<string>(
			name: "PerformanceLevelsJson",
			table: "AppAssessmentRubrics",
			type: "text",
			nullable: true);

		migrationBuilder.AddColumn<int>(
			name: "ScoringModel",
			table: "AppAssessmentRubrics",
			type: "integer",
			nullable: false,
			defaultValue: 0);

		migrationBuilder.AddColumn<string>(
			name: "StudentInstructions",
			table: "AppAssessmentRubrics",
			type: "character varying(2048)",
			maxLength: 2048,
			nullable: true);

		migrationBuilder.AddColumn<string>(
			name: "TagsJson",
			table: "AppAssessmentRubrics",
			type: "text",
			nullable: true);

		migrationBuilder.AddColumn<int>(
			name: "Visibility",
			table: "AppAssessmentRubrics",
			type: "integer",
			nullable: false,
			defaultValue: 3);

		migrationBuilder.CreateIndex(
			name: "IX_AppAssessmentRubrics_ScoringModel",
			table: "AppAssessmentRubrics",
			column: "ScoringModel");

		migrationBuilder.CreateIndex(
			name: "IX_AppAssessmentRubrics_Visibility",
			table: "AppAssessmentRubrics",
			column: "Visibility");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_AppAssessmentRubrics_ScoringModel",
			table: "AppAssessmentRubrics");

		migrationBuilder.DropIndex(
			name: "IX_AppAssessmentRubrics_Visibility",
			table: "AppAssessmentRubrics");

		migrationBuilder.DropColumn(
			name: "EvaluationInstructions",
			table: "AppAssessmentRubrics");

		migrationBuilder.DropColumn(
			name: "PerformanceLevelsJson",
			table: "AppAssessmentRubrics");

		migrationBuilder.DropColumn(
			name: "ScoringModel",
			table: "AppAssessmentRubrics");

		migrationBuilder.DropColumn(
			name: "StudentInstructions",
			table: "AppAssessmentRubrics");

		migrationBuilder.DropColumn(
			name: "TagsJson",
			table: "AppAssessmentRubrics");

		migrationBuilder.DropColumn(
			name: "Visibility",
			table: "AppAssessmentRubrics");
	}
}
