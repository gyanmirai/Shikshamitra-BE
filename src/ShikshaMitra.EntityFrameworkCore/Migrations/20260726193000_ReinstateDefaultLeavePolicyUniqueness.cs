using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

/// <summary>
/// Restores the tenant-null-safe uniqueness invariant for default leave policies.
/// </summary>
[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260726193000_ReinstateDefaultLeavePolicyUniqueness")]
public sealed class ReinstateDefaultLeavePolicyUniqueness : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			"""
			DO $migration$
			BEGIN
				IF EXISTS (
					SELECT 1
					FROM "AppEmployeeLeavePolicies"
					WHERE "IsDefault" = true
					GROUP BY
						COALESCE("TenantId", '00000000-0000-0000-0000-000000000000'::uuid),
						"AcademicYearId"
					HAVING COUNT(*) > 1
				) THEN
					RAISE EXCEPTION 'Cannot enforce one default leave policy per tenant and academic year because duplicate defaults already exist. Keep one default in each affected tenant and academic year before retrying the migration.';
				END IF;
			END
			$migration$;

			CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_Default"
			ON "AppEmployeeLeavePolicies" (
				COALESCE("TenantId", '00000000-0000-0000-0000-000000000000'::uuid),
				"AcademicYearId"
			)
			WHERE "IsDefault" = true;
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			"""
			DROP INDEX IF EXISTS "IX_EmployeeLeavePolicies_TenantId_AcademicYearId_Default";
			""");
	}
}
