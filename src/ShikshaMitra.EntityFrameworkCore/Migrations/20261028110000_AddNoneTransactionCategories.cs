using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261028110000_AddNoneTransactionCategories")]
public class AddNoneTransactionCategories : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Reuse existing IDs, including previously archived None choices. Historical references stay intact.
		migrationBuilder.Sql("""
			WITH ranked AS (
			  SELECT category."Id", row_number() OVER (
			    PARTITION BY category."TenantId", category."AcademicYearId", category."CategoryType"
			    ORDER BY category."IsDeleted", category."IsActive" DESC, category."CreationTime" DESC, category."Id") AS position
			  FROM "Finance_TransactionCategories" AS category
			  JOIN "academic_Years" AS year ON year."Id" = category."AcademicYearId"
			    AND (year."TenantId" = category."TenantId" OR (year."TenantId" IS NULL AND category."TenantId" IS NULL))
			  WHERE year."IsDeleted" = FALSE AND category."CategoryType" IN (0, 1)
			    AND upper(trim(category."Name")) = 'NONE'
			)
			UPDATE "Finance_TransactionCategories" AS category
			SET "IsActive" = TRUE, "IsDeleted" = FALSE, "DeletionTime" = NULL, "DeleterId" = NULL,
			    "EffectiveFrom" = NULL, "EffectiveTo" = NULL, "DeactivationReason" = NULL,
			    "Name" = 'None', "LastModificationTime" = CURRENT_TIMESTAMP,
			    "Version" = "Version" + 1,
			    "ConcurrencyStamp" = CAST(gen_random_uuid() AS text)
			WHERE category."Id" IN (SELECT "Id" FROM ranked WHERE position = 1)
			  AND (category."IsDeleted" OR NOT category."IsActive" OR category."Name" <> 'None'
			    OR category."EffectiveFrom" IS NOT NULL OR category."EffectiveTo" IS NOT NULL
			    OR category."DeactivationReason" IS NOT NULL);

			WITH kinds("CategoryType") AS (VALUES (0), (1))
			INSERT INTO "Finance_TransactionCategories"
			    ("Id", "TenantId", "AcademicYearId", "CategoryType", "Code", "NormalizedCode", "Name", "NormalizedName",
			     "Description", "IsActive", "Version", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
			SELECT gen_random_uuid(), year."TenantId", year."Id", kinds."CategoryType", NULL, NULL, 'None', 'NONE',
			       'No specific category.', TRUE, 1, '{}', CAST(gen_random_uuid() AS text), CURRENT_TIMESTAMP, FALSE
			FROM "academic_Years" AS year CROSS JOIN kinds
			WHERE year."IsDeleted" = FALSE AND NOT EXISTS (
			    SELECT 1 FROM "Finance_TransactionCategories" AS category
			    WHERE category."AcademicYearId" = year."Id" AND category."CategoryType" = kinds."CategoryType"
			      AND (category."TenantId" = year."TenantId" OR (category."TenantId" IS NULL AND year."TenantId" IS NULL))
			      AND upper(trim(category."Name")) = 'NONE');
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// Data-only forward fix: categories may already be referenced by payments, policies and fee heads.
	}
}
