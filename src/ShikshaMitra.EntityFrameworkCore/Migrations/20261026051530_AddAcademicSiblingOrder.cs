using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class AddAcademicSiblingOrder : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<int>(name: "SortOrder", table: "academic_EducationMediumStages", type: "integer", nullable: false, defaultValue: 0);
		migrationBuilder.AddColumn<int>(name: "SortOrder", table: "academic_Standards", type: "integer", nullable: false, defaultValue: 0);
		migrationBuilder.AddColumn<int>(name: "SortOrder", table: "academic_Divisions", type: "integer", nullable: false, defaultValue: 0);

		// Preserve existing sibling order once; subsequent reads use the saved positions.
		migrationBuilder.Sql("""
			WITH ordered AS (
				SELECT "Id", (ROW_NUMBER() OVER (
					PARTITION BY "TenantId", "EducationMediumId" ORDER BY "CreationTime", "Id") - 1)::integer AS position
				FROM "academic_EducationMediumStages"
			)
			UPDATE "academic_EducationMediumStages" AS stage SET "SortOrder" = ordered.position
			FROM ordered WHERE stage."Id" = ordered."Id";
			WITH ordered AS (
				SELECT "Id", (ROW_NUMBER() OVER (
					PARTITION BY "TenantId", "EducationMediumStageId" ORDER BY "CreationTime", "Id") - 1)::integer AS position
				FROM "academic_Standards"
			)
			UPDATE "academic_Standards" AS standard SET "SortOrder" = ordered.position
			FROM ordered WHERE standard."Id" = ordered."Id";
			WITH ordered AS (
				SELECT "Id", (ROW_NUMBER() OVER (
					PARTITION BY "TenantId", "StandardId" ORDER BY "CreationTime", "Id") - 1)::integer AS position
				FROM "academic_Divisions"
			)
			UPDATE "academic_Divisions" AS division SET "SortOrder" = ordered.position
			FROM ordered WHERE division."Id" = ordered."Id";
			""");

		migrationBuilder.CreateIndex(name: "IX_academic_EducationMediumStages_TenantId_EducationMediumId_S~",
			table: "academic_EducationMediumStages", columns: new[] { "TenantId", "EducationMediumId", "SortOrder" });
		migrationBuilder.CreateIndex(name: "IX_academic_Standards_TenantId_EducationMediumStageId_SortOrder",
			table: "academic_Standards", columns: new[] { "TenantId", "EducationMediumStageId", "SortOrder" });
		migrationBuilder.CreateIndex(name: "IX_academic_Divisions_TenantId_StandardId_SortOrder",
			table: "academic_Divisions", columns: new[] { "TenantId", "StandardId", "SortOrder" });
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(name: "IX_academic_EducationMediumStages_TenantId_EducationMediumId_S~", table: "academic_EducationMediumStages");
		migrationBuilder.DropIndex(name: "IX_academic_Standards_TenantId_EducationMediumStageId_SortOrder", table: "academic_Standards");
		migrationBuilder.DropIndex(name: "IX_academic_Divisions_TenantId_StandardId_SortOrder", table: "academic_Divisions");
		migrationBuilder.DropColumn(name: "SortOrder", table: "academic_EducationMediumStages");
		migrationBuilder.DropColumn(name: "SortOrder", table: "academic_Standards");
		migrationBuilder.DropColumn(name: "SortOrder", table: "academic_Divisions");
	}
}
