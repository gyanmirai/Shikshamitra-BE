using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260901010000_HardenConcessionTypeLifecycle")]
public class HardenConcessionTypeLifecycle : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.AddColumn<string>(
			name: "NormalizedName",
			table: "Finance_ConcessionTypes",
			type: "character varying(100)",
			maxLength: 100,
			nullable: true);

		migrationBuilder.AddColumn<bool>(
			name: "IsActive",
			table: "Finance_ConcessionTypes",
			type: "boolean",
			nullable: false,
			defaultValue: true);

		migrationBuilder.AddColumn<string>(
			name: "DeactivationReason",
			table: "Finance_ConcessionTypes",
			type: "character varying(500)",
			maxLength: 500,
			nullable: true);

		migrationBuilder.RenameColumn(
			name: "DocumentSize",
			table: "Finance_ConcessionTypeDocuments",
			newName: "MaxFileSizeBytes");

		migrationBuilder.AddColumn<bool>(
			name: "IsRequired",
			table: "Finance_ConcessionTypeDocuments",
			type: "boolean",
			nullable: false,
			defaultValue: true);

		migrationBuilder.AlterColumn<string>(
			name: "Name",
			table: "Finance_ConcessionTypes",
			type: "character varying(100)",
			maxLength: 100,
			nullable: false,
			oldClrType: typeof(string),
			oldType: "text");

		migrationBuilder.AlterColumn<string>(
			name: "Description",
			table: "Finance_ConcessionTypes",
			type: "character varying(500)",
			maxLength: 500,
			nullable: true,
			oldClrType: typeof(string),
			oldType: "text",
			oldNullable: true);

		migrationBuilder.AlterColumn<string>(
			name: "DocumentName",
			table: "Finance_ConcessionTypeDocuments",
			type: "character varying(100)",
			maxLength: 100,
			nullable: false,
			oldClrType: typeof(string),
			oldType: "text");

		migrationBuilder.Sql("""
			UPDATE "Finance_ConcessionTypes"
			SET "Name" = REGEXP_REPLACE(BTRIM("Name"), '\s+', ' ', 'g'),
			    "NormalizedName" = UPPER(REGEXP_REPLACE(BTRIM("Name"), '\s+', ' ', 'g'));

			DO $$
			BEGIN
			    IF EXISTS (
			        SELECT 1
			        FROM "Finance_ConcessionTypes"
			        WHERE "IsDeleted" = false
			        GROUP BY "TenantId", "AcademicYearId", "NormalizedName"
			        HAVING COUNT(*) > 1
			    ) THEN
			        RAISE EXCEPTION 'Duplicate concession type names exist after normalization. Resolve them before applying this migration.';
			    END IF;
			END $$;

			DELETE FROM "Finance_ConcessionTypeDocumentFormats" duplicate
			USING "Finance_ConcessionTypeDocumentFormats" keeper
			WHERE duplicate."ConcessionTypeDocumentId" = keeper."ConcessionTypeDocumentId"
			  AND duplicate."DocumentType" = keeper."DocumentType"
			  AND duplicate."Id" > keeper."Id";

			DO $$
			BEGIN
			    IF EXISTS (
			        SELECT 1
			        FROM "Finance_ConcessionRequests" request
			        LEFT JOIN "Finance_ConcessionTypes" type ON type."Id" = request."ConcessionTypeId"
			        WHERE type."Id" IS NULL
			    ) THEN
			        RAISE EXCEPTION 'Orphan concession requests exist. Restore their concession types before applying this migration.';
			    END IF;
			END $$;
			""");

		migrationBuilder.AlterColumn<string>(
			name: "NormalizedName",
			table: "Finance_ConcessionTypes",
			type: "character varying(100)",
			maxLength: 100,
			nullable: false,
			oldClrType: typeof(string),
			oldType: "character varying(100)",
			oldMaxLength: 100,
			oldNullable: true);

		migrationBuilder.CreateIndex(
			name: "UX_Finance_ConcessionTypes_Tenant_Year_NormalizedName",
			table: "Finance_ConcessionTypes",
			columns: new[] { "TenantId", "AcademicYearId", "NormalizedName" },
			unique: true,
			filter: "\"IsDeleted\" = false");

		migrationBuilder.CreateIndex(
			name: "UX_Finance_ConcessionTypeDocumentFormats_Document_Format",
			table: "Finance_ConcessionTypeDocumentFormats",
			columns: new[] { "ConcessionTypeDocumentId", "DocumentType" },
			unique: true);

		migrationBuilder.CreateIndex(
			name: "IX_Finance_ConcessionRequests_ConcessionTypeId",
			table: "Finance_ConcessionRequests",
			column: "ConcessionTypeId");

		migrationBuilder.AddForeignKey(
			name: "FK_Finance_ConcessionRequests_Finance_ConcessionTypes_ConcessionTypeId",
			table: "Finance_ConcessionRequests",
			column: "ConcessionTypeId",
			principalTable: "Finance_ConcessionTypes",
			principalColumn: "Id",
			onDelete: ReferentialAction.Restrict);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropForeignKey(
			name: "FK_Finance_ConcessionRequests_Finance_ConcessionTypes_ConcessionTypeId",
			table: "Finance_ConcessionRequests");

		migrationBuilder.DropIndex(
			name: "IX_Finance_ConcessionRequests_ConcessionTypeId",
			table: "Finance_ConcessionRequests");

		migrationBuilder.DropIndex(
			name: "UX_Finance_ConcessionTypeDocumentFormats_Document_Format",
			table: "Finance_ConcessionTypeDocumentFormats");

		migrationBuilder.DropIndex(
			name: "UX_Finance_ConcessionTypes_Tenant_Year_NormalizedName",
			table: "Finance_ConcessionTypes");

		migrationBuilder.DropColumn(name: "DeactivationReason", table: "Finance_ConcessionTypes");
		migrationBuilder.DropColumn(name: "IsActive", table: "Finance_ConcessionTypes");
		migrationBuilder.DropColumn(name: "NormalizedName", table: "Finance_ConcessionTypes");
		migrationBuilder.DropColumn(name: "IsRequired", table: "Finance_ConcessionTypeDocuments");

		migrationBuilder.RenameColumn(
			name: "MaxFileSizeBytes",
			table: "Finance_ConcessionTypeDocuments",
			newName: "DocumentSize");

		migrationBuilder.AlterColumn<string>(
			name: "Name",
			table: "Finance_ConcessionTypes",
			type: "text",
			nullable: false,
			oldClrType: typeof(string),
			oldType: "character varying(100)",
			oldMaxLength: 100);

		migrationBuilder.AlterColumn<string>(
			name: "Description",
			table: "Finance_ConcessionTypes",
			type: "text",
			nullable: true,
			oldClrType: typeof(string),
			oldType: "character varying(500)",
			oldMaxLength: 500,
			oldNullable: true);

		migrationBuilder.AlterColumn<string>(
			name: "DocumentName",
			table: "Finance_ConcessionTypeDocuments",
			type: "text",
			nullable: false,
			oldClrType: typeof(string),
			oldType: "character varying(100)",
			oldMaxLength: 100);
	}
}
