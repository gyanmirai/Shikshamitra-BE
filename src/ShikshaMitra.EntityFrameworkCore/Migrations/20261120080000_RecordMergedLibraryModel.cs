using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class RecordMergedLibraryModel : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// The earlier migrations from both branches already apply the schema changes.
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// This migration records the combined target model only.
	}
}
