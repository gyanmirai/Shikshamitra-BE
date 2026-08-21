using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260819103000_EnsureAdmissionSoftDeleteDefaults")]
public partial class EnsureAdmissionSoftDeleteDefaults : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			"""
			ALTER TABLE "AppAdmissionActivities" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			ALTER TABLE "AppAdmissionApplicants" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			ALTER TABLE "AppAdmissionApplicationWorkflowInstances" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			ALTER TABLE "AppAdmissionApplications" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			ALTER TABLE "AppAdmissionFamilies" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			ALTER TABLE "AppAdmissionFormDefinitions" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			ALTER TABLE "AppAdmissionFormInstances" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			ALTER TABLE "AppAdmissionGuardians" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			ALTER TABLE "AppAdmissionInquiries" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			ALTER TABLE "AppAdmissionNotes" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			ALTER TABLE "AppAdmissionWorkflowDefinitions" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			ALTER TABLE "AppAdmissionWorkflowTransitionHistory" ALTER COLUMN "IsDeleted" SET DEFAULT false;
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// These defaults reconcile the schema with ABP soft-delete invariants and
		// intentionally remain when this repair migration is rolled back.
	}
}
