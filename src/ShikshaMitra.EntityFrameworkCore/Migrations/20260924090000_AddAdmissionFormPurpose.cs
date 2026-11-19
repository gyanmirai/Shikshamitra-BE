using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260924090000_AddAdmissionFormPurpose")]
public partial class AddAdmissionFormPurpose : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_AppAdmissionFormDefinitions_TenantId_Code_Status",
			table: "AppAdmissionFormDefinitions");

		migrationBuilder.AddColumn<int>(
			name: "Purpose",
			table: "AppAdmissionFormDefinitions",
			type: "integer",
			nullable: false,
			defaultValue: 1);

		migrationBuilder.Sql("""
			UPDATE "AppAdmissionFormDefinitions"
			SET "Purpose" = CASE
				WHEN "Code" ILIKE '%INQUIRY%' OR "Name" ILIKE '%INQUIRY%' OR "Name" ILIKE '%ENQUIRY%' THEN 0
				WHEN "Code" ILIKE '%CORRECTION%' OR "Name" ILIKE '%CORRECTION%' THEN 2
				WHEN "Code" ILIKE '%CONSENT%' OR "Name" ILIKE '%CONSENT%' OR "Name" ILIKE '%DECLARATION%' THEN 3
				ELSE 1
			END;
			""");

		// Existing offering references are more reliable than a form's free-text name.
		// A form used in both slots needs an explicit school-specific split, not a silent guess.
		migrationBuilder.Sql("""
			DO $admission_form_purpose$
			BEGIN
				IF EXISTS (
					SELECT 1
					FROM "AppAdmissionOfferings" inquiry_offering
					JOIN "AppAdmissionOfferings" registration_offering
						ON registration_offering."ApplicationFormDefinitionId" = inquiry_offering."InquiryFormDefinitionId"
					WHERE inquiry_offering."InquiryFormDefinitionId" IS NOT NULL
						AND inquiry_offering."IsDeleted" = false
						AND registration_offering."IsDeleted" = false
				) THEN
					RAISE EXCEPTION 'An existing Admission Form is used as both Inquiry and Registration. Split its active Offering references before applying AddAdmissionFormPurpose.';
				END IF;
			END $admission_form_purpose$;

			UPDATE "AppAdmissionFormDefinitions" form_definition
			SET "Purpose" = 0
			WHERE EXISTS (
				SELECT 1 FROM "AppAdmissionOfferings" offering
				WHERE offering."InquiryFormDefinitionId" = form_definition."Id"
					AND offering."IsDeleted" = false
			);

			UPDATE "AppAdmissionFormDefinitions" form_definition
			SET "Purpose" = 1
			WHERE EXISTS (
				SELECT 1 FROM "AppAdmissionOfferings" offering
				WHERE offering."ApplicationFormDefinitionId" = form_definition."Id"
					AND offering."IsDeleted" = false
			);
			""");

		migrationBuilder.AlterColumn<int>(
			name: "Purpose",
			table: "AppAdmissionFormDefinitions",
			type: "integer",
			nullable: false,
			oldClrType: typeof(int),
			oldType: "integer",
			oldDefaultValue: 1);

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionFormDefinitions_TenantId_Purpose_Status",
			table: "AppAdmissionFormDefinitions",
			columns: new[] { "TenantId", "Purpose", "Status" });
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex(
			name: "IX_AppAdmissionFormDefinitions_TenantId_Purpose_Status",
			table: "AppAdmissionFormDefinitions");

		migrationBuilder.DropColumn(
			name: "Purpose",
			table: "AppAdmissionFormDefinitions");

		migrationBuilder.CreateIndex(
			name: "IX_AppAdmissionFormDefinitions_TenantId_Code_Status",
			table: "AppAdmissionFormDefinitions",
			columns: new[] { "TenantId", "Code", "Status" });
	}
}
