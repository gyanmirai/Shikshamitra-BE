using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
	/// <inheritdoc />
	public partial class AddGradeEntryWorkflowNotesV2 : Migration
	{
		/// <inheritdoc />
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppExamMarksWorkflowStates"
                    ADD COLUMN IF NOT EXISTS "ReviewerNote" text,
                    ADD COLUMN IF NOT EXISTS "UnlockReason" text;
                """);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "AppExamMarksWorkflowStates"
                    DROP COLUMN IF EXISTS "ReviewerNote",
                    DROP COLUMN IF EXISTS "UnlockReason";
                """);
		}
	}
}
