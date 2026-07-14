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
            AddTaskTypeSettingsColumns(migrationBuilder, "AppHomeworkTypes");
            AddTaskTypeSettingsColumns(migrationBuilder, "AppAssignmentTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropTaskTypeSettingsColumns(migrationBuilder, "AppHomeworkTypes");
            DropTaskTypeSettingsColumns(migrationBuilder, "AppAssignmentTypes");
        }

        private static void AddTaskTypeSettingsColumns(MigrationBuilder migrationBuilder, string tableName)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE IF EXISTS "{tableName}"
                    ADD COLUMN IF NOT EXISTS "DefaultDueDays" integer,
                    ADD COLUMN IF NOT EXISTS "DefaultEvaluationMethod" integer,
                    ADD COLUMN IF NOT EXISTS "DefaultMarks" numeric(18,2),
                    ADD COLUMN IF NOT EXISTS "DefaultSubmissionType" integer,
                    ADD COLUMN IF NOT EXISTS "Description" character varying(250),
                    ADD COLUMN IF NOT EXISTS "EvaluationRequired" boolean NOT NULL DEFAULT true,
                    ADD COLUMN IF NOT EXISTS "Status" integer NOT NULL DEFAULT 0;
                """);
        }

        private static void DropTaskTypeSettingsColumns(MigrationBuilder migrationBuilder, string tableName)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE IF EXISTS "{tableName}"
                    DROP COLUMN IF EXISTS "DefaultDueDays",
                    DROP COLUMN IF EXISTS "DefaultEvaluationMethod",
                    DROP COLUMN IF EXISTS "DefaultMarks",
                    DROP COLUMN IF EXISTS "DefaultSubmissionType",
                    DROP COLUMN IF EXISTS "Description",
                    DROP COLUMN IF EXISTS "EvaluationRequired",
                    DROP COLUMN IF EXISTS "Status";
                """);
        }
    }
}
