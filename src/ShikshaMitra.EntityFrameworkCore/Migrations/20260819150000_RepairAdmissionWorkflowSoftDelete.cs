using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260819150000_RepairAdmissionWorkflowSoftDelete")]
public partial class RepairAdmissionWorkflowSoftDelete : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Repair databases created before the soft-delete default migration. The
		// update must happen before enforcing the default so existing NULL rows do
		// not continue to violate the aggregate invariant.
		migrationBuilder.Sql(
			"UPDATE \"AppAdmissionWorkflowDefinitions\" SET \"IsDeleted\" = FALSE WHERE \"IsDeleted\" IS NULL;");
		migrationBuilder.Sql(
			"ALTER TABLE \"AppAdmissionWorkflowDefinitions\" ALTER COLUMN \"IsDeleted\" SET DEFAULT FALSE;");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// Do not reintroduce the invalid NULL state when rolling back.
	}
}
