using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations;

public partial class ReconcileAdmissionModel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The previous generated version recreated every ABP table. Reconciliation must be
        // safe for existing databases, so it is limited to the Admissions Campus source.
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS "AppAdmissionCampuses" (
                "Id" uuid NOT NULL,
                "TenantId" uuid NULL,
                "Code" character varying(64) NOT NULL,
                "Name" character varying(160) NOT NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "ExtraProperties" text NOT NULL DEFAULT '{}',
                "ConcurrencyStamp" character varying(40) NOT NULL DEFAULT '',
                "CreationTime" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "CreatorId" uuid NULL,
                "LastModificationTime" timestamp with time zone NULL,
                "LastModifierId" uuid NULL,
                "IsDeleted" boolean NOT NULL DEFAULT FALSE,
                "DeleterId" uuid NULL,
                "DeletionTime" timestamp with time zone NULL,
                CONSTRAINT "PK_AppAdmissionCampuses" PRIMARY KEY ("Id")
            );

            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "TenantId" uuid NULL;
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "Code" character varying(64) NOT NULL DEFAULT '';
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "Name" character varying(160) NOT NULL DEFAULT '';
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT TRUE;
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "ExtraProperties" text NOT NULL DEFAULT '{}';
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "ConcurrencyStamp" character varying(40) NOT NULL DEFAULT '';
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "CreationTime" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "CreatorId" uuid NULL;
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "LastModificationTime" timestamp with time zone NULL;
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "LastModifierId" uuid NULL;
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "IsDeleted" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "DeleterId" uuid NULL;
            ALTER TABLE "AppAdmissionCampuses" ADD COLUMN IF NOT EXISTS "DeletionTime" timestamp with time zone NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_AdmissionCampuses_Tenant_Code"
                ON "AppAdmissionCampuses" ("TenantId", "Code") WHERE "IsDeleted" = FALSE;
            CREATE INDEX IF NOT EXISTS "IX_AdmissionCampuses_Tenant_Active"
                ON "AppAdmissionCampuses" ("TenantId", "IsActive") WHERE "IsDeleted" = FALSE;

            ALTER TABLE "AppAdmissionOfferings" ADD COLUMN IF NOT EXISTS "CampusId" uuid NULL;

            DO $admission_campus_fk$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_AppAdmissionOfferings_AppAdmissionCampuses_CampusId'
                ) THEN
                    ALTER TABLE "AppAdmissionOfferings"
                        ADD CONSTRAINT "FK_AppAdmissionOfferings_AppAdmissionCampuses_CampusId"
                        FOREIGN KEY ("CampusId") REFERENCES "AppAdmissionCampuses" ("Id") ON DELETE RESTRICT;
                END IF;
            END
            $admission_campus_fk$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE IF EXISTS "AppAdmissionOfferings"
                DROP CONSTRAINT IF EXISTS "FK_AppAdmissionOfferings_AppAdmissionCampuses_CampusId";
            DROP INDEX IF EXISTS "IX_AdmissionCampuses_Tenant_Active";
            DROP INDEX IF EXISTS "UX_AdmissionCampuses_Tenant_Code";
            DROP TABLE IF EXISTS "AppAdmissionCampuses";
            """);
    }
}
