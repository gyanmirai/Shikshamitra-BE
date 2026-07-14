using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
	/// <inheritdoc />
	public partial class AddSmartExaminationSettings : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppGradingScales"
                    ADD COLUMN IF NOT EXISTS "Description" character varying(512),
                    ADD COLUMN IF NOT EXISTS "IsDefault" boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS "PassMarks" double precision,
                    ADD COLUMN IF NOT EXISTS "Status" integer NOT NULL DEFAULT 0;

                ALTER TABLE IF EXISTS "AppGradeRanges"
                    ADD COLUMN IF NOT EXISTS "GradePoint" double precision,
                    ADD COLUMN IF NOT EXISTS "Remark" text,
                    ADD COLUMN IF NOT EXISTS "ResultStatus" integer NOT NULL DEFAULT 0;

                ALTER TABLE IF EXISTS "AppExamTypes"
                    ADD COLUMN IF NOT EXISTS "AllowPracticalOral" boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS "Category" character varying(128),
                    ADD COLUMN IF NOT EXISTS "DefaultGradeScaleId" uuid,
                    ADD COLUMN IF NOT EXISTS "DefaultTotalMarks" double precision,
                    ADD COLUMN IF NOT EXISTS "DefaultWeightage" double precision,
                    ADD COLUMN IF NOT EXISTS "GradeScaleRequired" boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS "IncludedInResult" boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS "MarksEntryRequired" boolean NOT NULL DEFAULT false,
                    ADD COLUMN IF NOT EXISTS "Status" integer NOT NULL DEFAULT 0;
                """);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppGradingScales"
                    DROP COLUMN IF EXISTS "Description",
                    DROP COLUMN IF EXISTS "IsDefault",
                    DROP COLUMN IF EXISTS "PassMarks",
                    DROP COLUMN IF EXISTS "Status";

                ALTER TABLE IF EXISTS "AppGradeRanges"
                    DROP COLUMN IF EXISTS "GradePoint",
                    DROP COLUMN IF EXISTS "Remark",
                    DROP COLUMN IF EXISTS "ResultStatus";

                ALTER TABLE IF EXISTS "AppExamTypes"
                    DROP COLUMN IF EXISTS "AllowPracticalOral",
                    DROP COLUMN IF EXISTS "Category",
                    DROP COLUMN IF EXISTS "DefaultGradeScaleId",
                    DROP COLUMN IF EXISTS "DefaultTotalMarks",
                    DROP COLUMN IF EXISTS "DefaultWeightage",
                    DROP COLUMN IF EXISTS "GradeScaleRequired",
                    DROP COLUMN IF EXISTS "IncludedInResult",
                    DROP COLUMN IF EXISTS "MarksEntryRequired",
                    DROP COLUMN IF EXISTS "Status";
                """);
		}
	}
}
