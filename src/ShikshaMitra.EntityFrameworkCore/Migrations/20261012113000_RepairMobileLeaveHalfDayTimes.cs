using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class RepairMobileLeaveHalfDayTimes : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			ALTER TABLE "AppStudentLeaves"
				ADD COLUMN IF NOT EXISTS "HalfDayStartTime" time without time zone,
				ADD COLUMN IF NOT EXISTS "HalfDayEndTime" time without time zone;
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// The preceding migration's target model already owns these columns; rollback must preserve them.
	}
}
