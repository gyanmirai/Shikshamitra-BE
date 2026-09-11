using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260819100000_EnsureAdmissionWorkflowDescriptionColumn")]
public partial class EnsureAdmissionWorkflowDescriptionColumn : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// This is an idempotent schema reconciliation for databases that recorded
		// ProductionizeAdmissionWorkflowEngine before its Description column existed.
		migrationBuilder.Sql(
			"""
			ALTER TABLE "AppAdmissionWorkflowDefinitions"
			ADD COLUMN IF NOT EXISTS "Description" character varying(1024) NULL;
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// Intentionally retained: the preceding productionization migration owns this
		// model-required column, and this migration only reconciles schema drift.
	}
}
