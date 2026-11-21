using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class RecordMergedAcademicNotificationModel : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// The earlier Academic, Library and Payroll migrations already apply the combined schema.
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// This migration records only the combined target model.
	}
}
