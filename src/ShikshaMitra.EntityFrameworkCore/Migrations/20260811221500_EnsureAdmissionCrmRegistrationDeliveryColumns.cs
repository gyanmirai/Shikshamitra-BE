using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260811221500_EnsureAdmissionCrmRegistrationDeliveryColumns")]
public partial class EnsureAdmissionCrmRegistrationDeliveryColumns : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql(
			"""
			ALTER TABLE "AppAdmissionCrmRegistrationApplications"
			ADD COLUMN IF NOT EXISTS "AccessMode" integer NOT NULL DEFAULT 0;

			ALTER TABLE "AppAdmissionCrmRegistrationApplications"
			ALTER COLUMN "AccessMode" DROP DEFAULT;

			ALTER TABLE "AppAdmissionCrmMessageDeliveryLogs"
			ADD COLUMN IF NOT EXISTS "GeneratedDocumentType" integer NULL,
			ADD COLUMN IF NOT EXISTS "AttachmentLanguage" character varying(8) NULL,
			ADD COLUMN IF NOT EXISTS "AttachmentFileName" character varying(500) NULL,
			ADD COLUMN IF NOT EXISTS "AttachmentContentType" character varying(120) NULL;
			""");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// These columns are owned by the consolidated baseline. This compatibility migration
		// only repairs databases that recorded an earlier revision of that baseline.
	}
}
