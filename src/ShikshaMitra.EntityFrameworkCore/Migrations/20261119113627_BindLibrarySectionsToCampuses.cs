using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class BindLibrarySectionsToCampuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampusId",
                table: "AppLibrarySections",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibrarySections_CampusId",
                table: "AppLibrarySections",
                column: "CampusId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppLibrarySections_AppAdmissionCampuses_CampusId",
                table: "AppLibrarySections",
                column: "CampusId",
                principalTable: "AppAdmissionCampuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION sm_check_library_section_campus() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF OLD."CampusId" IS NOT NULL AND
                            (NEW."CampusId" IS DISTINCT FROM OLD."CampusId" OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId") THEN
                            RAISE EXCEPTION 'The recorded campus of a Library section cannot be replaced' USING ERRCODE = '23514';
                        END IF;
                        IF NEW."CampusId" IS NOT DISTINCT FROM OLD."CampusId" THEN RETURN NEW; END IF;
                    END IF;
                    IF NEW."CampusId" IS NOT NULL THEN
                        PERFORM 1 FROM "AppAdmissionCampuses" c
                        WHERE c."Id" = NEW."CampusId" AND c."TenantId" = NEW."TenantId" AND c."IsActive" AND NOT c."IsDeleted"
                        FOR SHARE;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'A Library section must use an active campus in the same school' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sm_check_library_section_campus BEFORE INSERT OR UPDATE OF "CampusId", "TenantId" ON "AppLibrarySections"
                    FOR EACH ROW EXECUTE FUNCTION sm_check_library_section_campus();

                CREATE FUNCTION sm_keep_library_campus_tenant() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."TenantId" IS DISTINCT FROM OLD."TenantId" AND EXISTS
                        (SELECT 1 FROM "AppLibrarySections" s WHERE s."CampusId" = OLD."Id") THEN
                        RAISE EXCEPTION 'A campus with Library sections cannot change school ownership' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sm_keep_library_campus_tenant BEFORE UPDATE OF "TenantId" ON "AppAdmissionCampuses"
                    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_campus_tenant();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE "AppLibrarySections" IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AppLibrarySections" WHERE "CampusId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Library section campus assignments must be preserved' USING ERRCODE = '23514';
                    END IF;
                END $$;
                DROP TRIGGER sm_keep_library_campus_tenant ON "AppAdmissionCampuses";
                DROP FUNCTION sm_keep_library_campus_tenant();
                DROP TRIGGER sm_check_library_section_campus ON "AppLibrarySections";
                DROP FUNCTION sm_check_library_section_campus();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_AppLibrarySections_AppAdmissionCampuses_CampusId",
                table: "AppLibrarySections");

            migrationBuilder.DropIndex(
                name: "IX_AppLibrarySections_CampusId",
                table: "AppLibrarySections");

            migrationBuilder.DropColumn(
                name: "CampusId",
                table: "AppLibrarySections");
        }
    }
}
