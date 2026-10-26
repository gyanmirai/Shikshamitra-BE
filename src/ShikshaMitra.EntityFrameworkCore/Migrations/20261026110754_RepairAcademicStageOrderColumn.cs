using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

[Migration("20261026110754_RepairAcademicStageOrderColumn")]
public class RepairAcademicStageOrderColumn : AddAcademicSiblingOrder
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			DO $repair$
			BEGIN
				LOCK TABLE "academic_EducationMediumStages" IN ACCESS EXCLUSIVE MODE;
				IF NOT EXISTS (
					SELECT 1 FROM pg_attribute
					WHERE attrelid = to_regclass('"academic_EducationMediumStages"')
						AND attname = 'SortOrder' AND NOT attisdropped
				) THEN
					ALTER TABLE "academic_EducationMediumStages"
						ADD COLUMN "SortOrder" integer NOT NULL DEFAULT 0;
					WITH ordered AS (
						SELECT "Id", (ROW_NUMBER() OVER (
							PARTITION BY "TenantId", "EducationMediumId" ORDER BY "CreationTime", "Id") - 1)::integer AS position
						FROM "academic_EducationMediumStages"
					)
					UPDATE "academic_EducationMediumStages" AS stage SET "SortOrder" = ordered.position
					FROM ordered WHERE stage."Id" = ordered."Id";
				END IF;
			END
			$repair$;
			CREATE INDEX IF NOT EXISTS "IX_academic_EducationMediumStages_TenantId_EducationMediumId_S~"
				ON "academic_EducationMediumStages" ("TenantId", "EducationMediumId", "SortOrder");
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// The preceding migration's target model already owns this column and index; preserve their data on rollback.
	}
}
