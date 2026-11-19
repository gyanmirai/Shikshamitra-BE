using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260929120000_EnsureFinancePolicyDefaults")]
public partial class EnsureFinancePolicyDefaults : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default\";");

		// Keep the existing default when present; otherwise choose the oldest active policy.
		// Collection used to permit multiple active defaults because its index targeted Archived.
		foreach (var (table, activeStatus) in new[]
		{
			("Finance_ConcessionPolicies", 1),
			("Finance_RefundPolicies", 1),
			("Finance_CollectionPolicies", 0),
			("Finance_ProrationPolicies", 1)
		})
		{
			migrationBuilder.Sql($"""
				WITH ranked AS (
				    SELECT "Id",
				           ROW_NUMBER() OVER (
				               PARTITION BY "TenantId", "AcademicYearId"
				               ORDER BY "IsDefault" DESC, "CreationTime", "Id"
				           ) AS position
				    FROM "{table}"
				    WHERE "IsDeleted" = false AND "Status" = {activeStatus}
				)
				UPDATE "{table}" AS policy
				SET "IsDefault" = (ranked.position = 1)
				FROM ranked
				WHERE policy."Id" = ranked."Id"
				  AND policy."IsDefault" IS DISTINCT FROM (ranked.position = 1);
				""");
		}

		migrationBuilder.Sql("""
			CREATE UNIQUE INDEX "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default"
			ON "Finance_CollectionPolicies" ("TenantId", "AcademicYearId", "IsDefault")
			WHERE "IsDeleted" = false AND "Status" = 0 AND "IsDefault" = true;
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default\";");
		migrationBuilder.Sql("""
			CREATE UNIQUE INDEX "IX_Finance_CollectionPolicies_Tenant_AcademicYear_Default"
			ON "Finance_CollectionPolicies" ("TenantId", "AcademicYearId", "IsDefault")
			WHERE "IsDeleted" = false AND "Status" = 1 AND "IsDefault" = true;
			""");
	}
}
