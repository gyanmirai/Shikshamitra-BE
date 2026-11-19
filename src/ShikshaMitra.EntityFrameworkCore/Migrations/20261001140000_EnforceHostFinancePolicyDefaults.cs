using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261001140000_EnforceHostFinancePolicyDefaults")]
public partial class EnforceHostFinancePolicyDefaults : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		foreach (var (family, activeStatus) in new[]
		{
			("Concession", 1), ("Refund", 1), ("Collection", 0), ("Proration", 1)
		})
		{
			var table = $"Finance_{family}Policies";
			// The earlier backfill may have run before host policies were created.
			// Keep their existing default; otherwise choose the oldest active policy.
			migrationBuilder.Sql($"""
				WITH ranked AS (
				    SELECT "Id", ROW_NUMBER() OVER (
				        PARTITION BY "AcademicYearId"
				        ORDER BY "IsDefault" DESC, "CreationTime", "Id") AS position
				    FROM "{table}"
				    WHERE "TenantId" IS NULL AND "IsDeleted" = false AND "Status" = {activeStatus}
				)
				UPDATE "{table}" AS policy
				SET "IsDefault" = (ranked.position = 1)
				FROM ranked
				WHERE policy."Id" = ranked."Id"
				  AND policy."IsDefault" IS DISTINCT FROM (ranked.position = 1);
				""");
			migrationBuilder.CreateIndex(
				name: $"IX_{table}_Host_ActiveDefault", table: table,
				column: "AcademicYearId", unique: true,
				filter: $"\"TenantId\" IS NULL AND \"IsDeleted\" = false AND \"Status\" = {activeStatus} AND \"IsDefault\" = true");
		}
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		foreach (var family in new[] { "Concession", "Refund", "Collection", "Proration" })
			migrationBuilder.DropIndex($"IX_Finance_{family}Policies_Host_ActiveDefault", $"Finance_{family}Policies");
		// Preserve the repaired defaults; rolling back the guard must not restore duplicates.
	}
}
