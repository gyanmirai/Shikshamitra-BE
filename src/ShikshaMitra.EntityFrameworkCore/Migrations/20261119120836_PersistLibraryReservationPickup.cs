using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShikshaMitra.Migrations
{
    /// <inheritdoc />
    public partial class PersistLibraryReservationPickup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PickupCampusId",
                table: "AppBookReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupLabel",
                table: "AppBookReservations",
                type: "character varying(384)",
                maxLength: 384,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PickupSectionId",
                table: "AppBookReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookReservations_TenantId_PickupSectionId_State",
                table: "AppBookReservations",
                columns: new[] { "TenantId", "PickupSectionId", "State" },
                filter: "\"PickupSectionId\" IS NOT NULL");

            migrationBuilder.Sql("""
                ALTER TABLE "AppBookReservations" ADD CONSTRAINT "CK_LibraryReservation_Pickup"
                    CHECK (("PickupSectionId" IS NULL AND "PickupCampusId" IS NULL AND "PickupLabel" IS NULL)
                        OR ("PickupSectionId" IS NOT NULL AND "PickupCampusId" IS NOT NULL
                            AND "PickupLabel" IS NOT NULL AND length(trim("PickupLabel")) > 0));
                CREATE FUNCTION sm_check_library_reservation_pickup() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF (NEW."PickupSectionId", NEW."PickupCampusId", NEW."PickupLabel") IS DISTINCT FROM
                            (OLD."PickupSectionId", OLD."PickupCampusId", OLD."PickupLabel")
                            OR (OLD."PickupSectionId" IS NOT NULL AND NEW."TenantId" IS DISTINCT FROM OLD."TenantId") THEN
                            RAISE EXCEPTION 'The original reservation pickup location must be preserved' USING ERRCODE = '23514';
                        END IF;
                        RETURN NEW;
                    END IF;
                    IF NEW."PickupSectionId" IS NOT NULL THEN
                        PERFORM 1 FROM "AppLibrarySections" s JOIN "AppAdmissionCampuses" c ON c."Id" = s."CampusId"
                        WHERE s."Id" = NEW."PickupSectionId" AND s."TenantId" = NEW."TenantId"
                          AND c."Id" = NEW."PickupCampusId" AND c."TenantId" = NEW."TenantId" AND c."IsActive" AND NOT c."IsDeleted"
                        FOR SHARE OF s, c;
                        IF NOT FOUND THEN
                            RAISE EXCEPTION 'The reservation needs an active pickup location in its own school' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sm_check_reservation_pickup BEFORE INSERT OR UPDATE ON "AppBookReservations"
                    FOR EACH ROW EXECUTE FUNCTION sm_check_library_reservation_pickup();

                CREATE FUNCTION sm_check_library_hold_pickup() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."State" = 0 AND EXISTS (SELECT 1 FROM "AppBookReservations" r
                        WHERE r."Id" = NEW."ReservationId" AND r."PickupSectionId" IS NOT NULL AND NOT EXISTS
                          (SELECT 1 FROM "AppBookCopies" c WHERE c."Id" = NEW."CopyId" AND c."TenantId" = r."TenantId"
                            AND c."SectionId" = r."PickupSectionId" AND c."BookId" = r."BookId")) THEN
                        RAISE EXCEPTION 'Allocate the hold at the recorded pickup section' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sm_check_hold_pickup BEFORE INSERT OR UPDATE ON "AppLibraryHolds"
                    FOR EACH ROW EXECUTE FUNCTION sm_check_library_hold_pickup();

                CREATE FUNCTION sm_keep_library_pickup_location() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_TABLE_NAME = 'AppLibrarySections' THEN
                        IF EXISTS (SELECT 1 FROM "AppBookReservations" r WHERE r."PickupSectionId" = OLD."Id" AND r."State" IN (0, 1, 5)) THEN
                            RAISE EXCEPTION 'The section is required by an active pickup reservation' USING ERRCODE = '23514';
                        END IF;
                        RETURN OLD;
                    END IF;
                    IF OLD."SectionId" IS DISTINCT FROM NEW."SectionId" AND EXISTS (SELECT 1 FROM "AppLibraryHolds" h
                        JOIN "AppBookReservations" r ON r."Id" = h."ReservationId" AND r."TenantId" = h."TenantId"
                        WHERE h."CopyId" = OLD."Id" AND h."TenantId" = OLD."TenantId" AND h."State" = 0
                          AND r."PickupSectionId" IS NOT NULL AND r."PickupSectionId" IS DISTINCT FROM NEW."SectionId") THEN
                        RAISE EXCEPTION 'The held accession must stay at the recorded pickup section' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER sm_keep_reservation_pickup_site BEFORE DELETE ON "AppLibrarySections"
                    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_pickup_location();
                CREATE TRIGGER sm_keep_hold_copy_at_pickup BEFORE UPDATE OF "SectionId" ON "AppBookCopies"
                    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_pickup_location();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE "AppBookReservations" IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AppBookReservations" WHERE "PickupSectionId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Retain recorded reservation pickup locations during rollback' USING ERRCODE = '23514';
                    END IF;
                END $$;
                DROP TRIGGER sm_check_reservation_pickup ON "AppBookReservations";
                DROP TRIGGER sm_check_hold_pickup ON "AppLibraryHolds";
                DROP TRIGGER sm_keep_reservation_pickup_site ON "AppLibrarySections";
                DROP TRIGGER sm_keep_hold_copy_at_pickup ON "AppBookCopies";
                DROP FUNCTION sm_check_library_reservation_pickup();
                DROP FUNCTION sm_check_library_hold_pickup();
                DROP FUNCTION sm_keep_library_pickup_location();
                ALTER TABLE "AppBookReservations" DROP CONSTRAINT "CK_LibraryReservation_Pickup";
                """);
            migrationBuilder.DropIndex(
                name: "IX_AppBookReservations_TenantId_PickupSectionId_State",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "PickupCampusId",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "PickupLabel",
                table: "AppBookReservations");

            migrationBuilder.DropColumn(
                name: "PickupSectionId",
                table: "AppBookReservations");
        }
    }
}
