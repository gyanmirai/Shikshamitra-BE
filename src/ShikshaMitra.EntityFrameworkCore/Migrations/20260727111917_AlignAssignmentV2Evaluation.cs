using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

/// <inheritdoc />
public partial class AlignAssignmentV2Evaluation : Migration
{
	/// <inheritdoc />
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<int>(
			name: "EvaluationMethod",
			table: "AppAssignments",
			type: "integer",
			nullable: true);

			migrationBuilder.AddColumn<bool>(
				name: "EvaluationRequired",
				table: "AppAssignments",
				type: "boolean",
				nullable: true);

			migrationBuilder.Sql("""
				UPDATE "AppAssignments" AS a
				SET "EducationMediumStageId" = s."EducationMediumStageId",
				    "EducationMediumId" = ms."EducationMediumId",
				    "EducationBoardId" = m."EducationBoardId",
				    "AcademicYearId" = b."AcademicYearId"
				FROM "AppStandards" AS s
				JOIN "AppEducationMediumStages" AS ms ON ms."Id" = s."EducationMediumStageId"
				JOIN "AppEducationMediums" AS m ON m."Id" = ms."EducationMediumId"
				JOIN "AppEducationBoards" AS b ON b."Id" = m."EducationBoardId"
				WHERE a."StandardId" = s."Id"
				  AND (a."AcademicYearId" IS NULL
				       OR a."EducationBoardId" IS NULL
				       OR a."EducationMediumId" IS NULL
				       OR a."EducationMediumStageId" IS NULL);
				""");

	}

	/// <inheritdoc />
	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropColumn(
			name: "EvaluationMethod",
			table: "AppAssignments");

		migrationBuilder.DropColumn(
			name: "EvaluationRequired",
			table: "AppAssignments");
	}
}
