using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260903153000_RemoveLegacyConcessionPolicyAllowMultipleConcessions")]
public partial class RemoveLegacyConcessionPolicyAllowMultipleConcessions : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// This column existed in an earlier database shape but is not part of the
		// current concession-policy contract or aggregate. Its NOT NULL constraint
		// prevents EF inserts because the current model correctly does not write it.
		migrationBuilder.Sql("""
			ALTER TABLE "Finance_ConcessionPolicies"
			DROP COLUMN IF EXISTS "AllowMultipleConcessions";
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			ALTER TABLE "Finance_ConcessionPolicies"
			ADD COLUMN IF NOT EXISTS "AllowMultipleConcessions" boolean NOT NULL DEFAULT FALSE;
			""");
	}
}
