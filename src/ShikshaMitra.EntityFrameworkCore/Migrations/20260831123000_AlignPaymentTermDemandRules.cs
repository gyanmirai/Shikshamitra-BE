using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

/// <summary>
/// Converts legacy rule 0 (immediately on assignment) templates to the new
/// rule 0 meaning (on installment start date). Issued DueFee snapshots are
/// intentionally untouched.
/// </summary>
[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260831123000_AlignPaymentTermDemandRules")]
public class AlignPaymentTermDemandRules : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<int>(
			name: "DemandGenerationRule",
			table: "Finance_PaymentTermVersions",
			type: "integer",
			nullable: false,
			defaultValue: 0);

		migrationBuilder.AddColumn<int?>(
			name: "DemandOffsetDays",
			table: "Finance_PaymentTermVersions",
			type: "integer",
			nullable: true);

		migrationBuilder.AddColumn<int>(
			name: "HolidayShiftingMethod",
			table: "Finance_PaymentTermVersions",
			type: "integer",
			nullable: false,
			defaultValue: 0);

		migrationBuilder.Sql("""
			UPDATE "Finance_PaymentTermVersions" AS version
			SET "DemandGenerationRule" = term."DemandGenerationRule",
			    "DemandOffsetDays" = term."DemandOffsetDays",
			    "HolidayShiftingMethod" = term."HolidayShiftingMethod"
			FROM "Finance_PaymentTerms" AS term
			WHERE version."PaymentTermId" = term."Id";
			""");

		migrationBuilder.Sql("""
			UPDATE "Finance_PaymentTermInstallments" AS installment
			SET "DemandDate" = installment."CoverageStartDate"
			FROM "Finance_PaymentTermVersions" AS version,
			     "Finance_PaymentTerms" AS term
			WHERE installment."PaymentTermVersionId" = version."Id"
			  AND version."PaymentTermId" = term."Id"
			  AND term."DemandGenerationRule" = 0;
			""");

		// Normalize any legacy duplicate defaults before adding the database guard.
		// The most recently changed record remains the default for each board/year.
		migrationBuilder.Sql("""
			WITH ranked_defaults AS (
			    SELECT "Id",
			           ROW_NUMBER() OVER (
			               PARTITION BY "TenantId", "AcademicYearId", "BoardId"
			               ORDER BY "LastModificationTime" DESC NULLS LAST,
			                        "CreationTime" DESC,
			                        "Id" DESC
			           ) AS default_rank
			    FROM "Finance_PaymentTerms"
			    WHERE "IsDeleted" = false AND "IsDefault" = true
			)
			UPDATE "Finance_PaymentTerms" AS term
			SET "IsDefault" = false
			FROM ranked_defaults AS ranked
			WHERE term."Id" = ranked."Id" AND ranked.default_rank > 1;
			""");

		migrationBuilder.DropIndex(
			name: "IX_Finance_PaymentTerms_TenantId_AcademicYearId_BoardId_IsDefa~",
			table: "Finance_PaymentTerms");

		migrationBuilder.CreateIndex(
			name: "IX_Finance_PaymentTerms_OneDefaultPerBoard",
			table: "Finance_PaymentTerms",
			columns: new[] { "TenantId", "AcademicYearId", "BoardId" },
			unique: true,
			filter: "\"IsDeleted\" = false AND \"IsDefault\" = true");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_Finance_PaymentTerms_OneDefaultPerBoard",
			table: "Finance_PaymentTerms");

		migrationBuilder.CreateIndex(
			name: "IX_Finance_PaymentTerms_TenantId_AcademicYearId_BoardId_IsDefa~",
			table: "Finance_PaymentTerms",
			columns: new[] { "TenantId", "AcademicYearId", "BoardId", "IsDefault" });

		// The legacy assignment-time date was not stored independently and cannot
		// be reconstructed. Rolling back code keeps the normalized template dates.
		migrationBuilder.DropColumn(
			name: "DemandGenerationRule",
			table: "Finance_PaymentTermVersions");

		migrationBuilder.DropColumn(
			name: "DemandOffsetDays",
			table: "Finance_PaymentTermVersions");

		migrationBuilder.DropColumn(
			name: "HolidayShiftingMethod",
			table: "Finance_PaymentTermVersions");
	}
}
