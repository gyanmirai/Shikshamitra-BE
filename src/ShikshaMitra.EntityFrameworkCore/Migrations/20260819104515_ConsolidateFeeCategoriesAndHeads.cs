using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260819104515_ConsolidateFeeCategoriesAndHeads")]
public partial class ConsolidateFeeCategoriesAndHeads : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_Finance_FeeCategories_TenantId_AcademicYearId_Name",
			table: "Finance_FeeCategories");
		migrationBuilder.DropIndex(
			name: "IX_Finance_FeeHeads_TenantId_AcademicYearId_FeeCategoryId_Name",
			table: "Finance_FeeHeads");

		migrationBuilder.AddColumn<string>(name: "NormalizedName", table: "Finance_FeeCategories", type: "character varying(100)", maxLength: 100, nullable: true);
		migrationBuilder.AddColumn<string>(name: "NormalizedName", table: "Finance_FeeHeads", type: "character varying(40)", maxLength: 40, nullable: true);
		migrationBuilder.AddColumn<Guid>(name: "RevisionGroupId", table: "Finance_FeeHeads", type: "uuid", nullable: true);
		migrationBuilder.AddColumn<int>(name: "RevisionNumber", table: "Finance_FeeHeads", type: "integer", nullable: false, defaultValue: 1);
		migrationBuilder.AddColumn<Guid>(name: "PreviousRevisionId", table: "Finance_FeeHeads", type: "uuid", nullable: true);
		migrationBuilder.AddColumn<Guid>(name: "SuccessorRevisionId", table: "Finance_FeeHeads", type: "uuid", nullable: true);
		migrationBuilder.AddColumn<bool>(name: "IsCurrentRevision", table: "Finance_FeeHeads", type: "boolean", nullable: false, defaultValue: true);
		migrationBuilder.AddColumn<string>(name: "RevisionReason", table: "Finance_FeeHeads", type: "character varying(500)", maxLength: 500, nullable: true);

		migrationBuilder.Sql("""
			UPDATE "Finance_FeeCategories"
			SET "NormalizedName" = UPPER(TRIM(REGEXP_REPLACE("Name", '\s+', ' ', 'g')));
			UPDATE "Finance_FeeHeads"
			SET "NormalizedName" = UPPER(TRIM(REGEXP_REPLACE("Name", '\s+', ' ', 'g'))),
			    "RevisionGroupId" = "Id";
			""");

		migrationBuilder.Sql("""
			DO $$
			BEGIN
				IF EXISTS (
					SELECT 1 FROM "Finance_FeeCategories"
					WHERE NOT "IsDeleted"
					GROUP BY "TenantId", "AcademicYearId", "NormalizedName" HAVING COUNT(*) > 1
				) OR EXISTS (
					SELECT 1 FROM "Finance_FeeHeads"
					WHERE NOT "IsDeleted" AND "IsCurrentRevision"
					GROUP BY "TenantId", "AcademicYearId", "FeeCategoryId", "NormalizedName" HAVING COUNT(*) > 1
				) THEN
					RAISE EXCEPTION 'Fee category or fee head names contain case-insensitive duplicates. Resolve them before applying this migration.';
				END IF;
			END $$;
			""");

		migrationBuilder.AlterColumn<string>(name: "NormalizedName", table: "Finance_FeeCategories", type: "character varying(100)", maxLength: 100, nullable: false, oldClrType: typeof(string), oldType: "character varying(100)", oldMaxLength: 100, oldNullable: true);
		migrationBuilder.AlterColumn<string>(name: "NormalizedName", table: "Finance_FeeHeads", type: "character varying(40)", maxLength: 40, nullable: false, oldClrType: typeof(string), oldType: "character varying(40)", oldMaxLength: 40, oldNullable: true);
		migrationBuilder.AlterColumn<Guid>(name: "RevisionGroupId", table: "Finance_FeeHeads", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

		migrationBuilder.CreateIndex(
			name: "IX_Finance_FeeCategories_TenantId_AcademicYearId_NormalizedName",
			table: "Finance_FeeCategories",
			columns: new[] { "TenantId", "AcademicYearId", "NormalizedName" },
			unique: true,
			filter: "\"IsDeleted\" = FALSE");
		migrationBuilder.CreateIndex(
			name: "IX_Finance_FeeHeads_TenantId_AcademicYearId_FeeCategoryId_NormalizedName",
			table: "Finance_FeeHeads",
			columns: new[] { "TenantId", "AcademicYearId", "FeeCategoryId", "NormalizedName" },
			unique: true,
			filter: "\"IsDeleted\" = FALSE AND \"IsCurrentRevision\" = TRUE");
		migrationBuilder.CreateIndex(
			name: "IX_Finance_FeeHeads_RevisionGroupId_RevisionNumber",
			table: "Finance_FeeHeads",
			columns: new[] { "RevisionGroupId", "RevisionNumber" },
			unique: true);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(name: "IX_Finance_FeeCategories_TenantId_AcademicYearId_NormalizedName", table: "Finance_FeeCategories");
		migrationBuilder.DropIndex(name: "IX_Finance_FeeHeads_TenantId_AcademicYearId_FeeCategoryId_NormalizedName", table: "Finance_FeeHeads");
		migrationBuilder.DropIndex(name: "IX_Finance_FeeHeads_RevisionGroupId_RevisionNumber", table: "Finance_FeeHeads");
		migrationBuilder.DropColumn(name: "NormalizedName", table: "Finance_FeeCategories");
		migrationBuilder.DropColumn(name: "NormalizedName", table: "Finance_FeeHeads");
		migrationBuilder.DropColumn(name: "RevisionGroupId", table: "Finance_FeeHeads");
		migrationBuilder.DropColumn(name: "RevisionNumber", table: "Finance_FeeHeads");
		migrationBuilder.DropColumn(name: "PreviousRevisionId", table: "Finance_FeeHeads");
		migrationBuilder.DropColumn(name: "SuccessorRevisionId", table: "Finance_FeeHeads");
		migrationBuilder.DropColumn(name: "IsCurrentRevision", table: "Finance_FeeHeads");
		migrationBuilder.DropColumn(name: "RevisionReason", table: "Finance_FeeHeads");
		migrationBuilder.CreateIndex(name: "IX_Finance_FeeCategories_TenantId_AcademicYearId_Name", table: "Finance_FeeCategories", columns: new[] { "TenantId", "AcademicYearId", "Name" });
		migrationBuilder.CreateIndex(name: "IX_Finance_FeeHeads_TenantId_AcademicYearId_FeeCategoryId_Name", table: "Finance_FeeHeads", columns: new[] { "TenantId", "AcademicYearId", "FeeCategoryId", "Name" });
	}
}
