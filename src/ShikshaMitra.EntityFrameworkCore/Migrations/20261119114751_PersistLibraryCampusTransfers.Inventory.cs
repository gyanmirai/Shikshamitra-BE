using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class PersistLibraryCampusTransfers
{
	private static void AddTransferInventoryGuards(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		CREATE FUNCTION sm_check_library_transfer_copy() RETURNS trigger LANGUAGE plpgsql AS $$
		DECLARE copy_id uuid;
		BEGIN
		    IF TG_TABLE_NAME = 'AppBookCopies' THEN copy_id := NEW."Id";
		    ELSE copy_id := NEW."CopyId"; END IF;
		    IF NOT EXISTS (SELECT 1 FROM "AppBookCopies" c LEFT JOIN "AppLibraryCampusTransferItems" i
		        ON i."CopyId" = c."Id" AND i."TenantId" = c."TenantId" AND i."ReceiptId" IS NULL
		        LEFT JOIN "AppLibraryCampusTransfers" h ON h."Id" = i."TransferId" AND h."TenantId" = i."TenantId"
		        WHERE c."Id" = copy_id AND ((c."Status" <> 8 AND i."Id" IS NULL) OR
		            (c."Status" = 8 AND i."Id" IS NOT NULL AND c."BookId" = i."BookId" AND c."AccessionNumber" = i."AccessionNumber"
		             AND c."SectionId" = h."SourceSectionId" AND c."ShelfId" = i."SourceShelfId"))) THEN
		        RAISE EXCEPTION 'The copy register must agree with the outstanding campus transfer' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE CONSTRAINT TRIGGER sm_check_transfer_copy AFTER INSERT OR UPDATE ON "AppBookCopies"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sm_check_library_transfer_copy();
		CREATE CONSTRAINT TRIGGER sm_check_transfer_item_copy AFTER INSERT OR UPDATE ON "AppLibraryCampusTransferItems"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sm_check_library_transfer_copy();

		CREATE FUNCTION sm_check_library_cross_campus_move() RETURNS trigger LANGUAGE plpgsql AS $$
		DECLARE source_campus uuid;
		DECLARE destination_campus uuid;
		BEGIN
		    SELECT "CampusId" INTO source_campus FROM "AppLibrarySections" WHERE "Id" = OLD."SectionId" AND "TenantId" IS NOT DISTINCT FROM OLD."TenantId";
		    SELECT "CampusId" INTO destination_campus FROM "AppLibrarySections" WHERE "Id" = NEW."SectionId" AND "TenantId" IS NOT DISTINCT FROM NEW."TenantId";
		    IF source_campus IS DISTINCT FROM destination_campus AND (OLD."Status" <> 8 OR
		        NOT EXISTS (SELECT 1 FROM "AppLibraryCampusTransferItems" i
		            JOIN "AppLibraryCampusTransfers" h ON h."Id" = i."TransferId" AND h."TenantId" = i."TenantId"
		            JOIN "AppLibraryCopyEvents" e ON e."Id" = i."ReceiptEventId"
		            WHERE i."CopyId" = NEW."Id" AND i."TenantId" = NEW."TenantId"
		              AND h."SourceSectionId" = OLD."SectionId" AND i."SourceShelfId" = OLD."ShelfId"
		              AND h."DestinationSectionId" = NEW."SectionId" AND h."DestinationShelfId" = NEW."ShelfId"
		              AND i."Condition" = NEW."Status" AND e."Sequence" > (SELECT d."Sequence" FROM "AppLibraryCopyEvents" d WHERE d."Id" = i."DispatchEventId")
		              AND NOT EXISTS (SELECT 1 FROM "AppLibraryCopyEvents" f WHERE f."CopyId" = e."CopyId"
		                  AND f."TenantId" = e."TenantId" AND f."EventType" = 11 AND f."Sequence" > e."Sequence"))) THEN
		        RAISE EXCEPTION 'A cross-campus location change requires a recorded transfer receipt' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE CONSTRAINT TRIGGER sm_check_cross_campus_move AFTER UPDATE ON "AppBookCopies"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW WHEN (OLD."SectionId" IS DISTINCT FROM NEW."SectionId")
		    EXECUTE FUNCTION sm_check_library_cross_campus_move();

		CREATE FUNCTION sm_keep_library_transfer_destination() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    IF EXISTS (SELECT 1 FROM "AppLibraryCampusTransfers" h
		        JOIN "AppLibraryCampusTransferItems" i ON i."TransferId" = h."Id" AND i."TenantId" = h."TenantId"
		        WHERE h."DestinationShelfId" = OLD."Id" AND i."ReceiptId" IS NULL) THEN
		        RAISE EXCEPTION 'Outstanding transfers require their recorded destination shelf' USING ERRCODE = '23514';
		    END IF;
		    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
		    RETURN NEW;
		END $$;
		CREATE TRIGGER sm_keep_transfer_destination BEFORE DELETE OR UPDATE OF "SectionId" ON "AppLibraryShelves"
		    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_transfer_destination();

		CREATE FUNCTION sm_check_library_stock_transfer() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    IF NEW."Source" <> 1 OR NEW."CurrentStatus" IS DISTINCT FROM 8 OR NOT EXISTS
		        (SELECT 1 FROM "AppLibraryCampusTransferItems" i JOIN "AppLibraryCampusTransfers" h
		          ON h."Id" = i."TransferId" AND h."TenantId" = i."TenantId"
		         JOIN "AppBookCopies" c ON c."Id" = i."CopyId" AND c."TenantId" = i."TenantId"
		         WHERE i."CopyId" = NEW."RowId" AND i."TenantId" = NEW."TenantId" AND i."ReceiptId" IS NULL
		           AND i."AccessionNumber" = NEW."AccessionNumber"
		           AND c."Status" = 8 AND c."SectionId" = h."SourceSectionId" AND c."ShelfId" = i."SourceShelfId") THEN
		        RAISE EXCEPTION 'An in-transit finding requires the active campus transfer' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE TRIGGER sm_check_stock_transfer BEFORE INSERT ON "AppLibraryStockVerificationResults"
		    FOR EACH ROW WHEN (NEW."Finding" = 17) EXECUTE FUNCTION sm_check_library_stock_transfer();
		""");

	private static void RemoveTransferGuards(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		LOCK TABLE "AppLibraryCampusTransfers", "AppLibraryCampusTransferItems", "AppLibraryCampusTransferReceipts",
		    "AppBookCopies", "AppLibraryStockVerificationResults" IN ACCESS EXCLUSIVE MODE;
		DO $$ BEGIN
		    IF EXISTS (SELECT 1 FROM "AppLibraryCampusTransfers") OR EXISTS (SELECT 1 FROM "AppLibraryStockVerificationResults" WHERE "Finding" = 17) THEN
		        RAISE EXCEPTION 'Campus transfer evidence must be preserved during rollback' USING ERRCODE = '23514';
		    END IF;
		END $$;
		DROP TRIGGER sm_check_stock_transfer ON "AppLibraryStockVerificationResults";
		DROP TRIGGER sm_keep_transfer_destination ON "AppLibraryShelves";
		DROP TRIGGER sm_check_cross_campus_move ON "AppBookCopies";
		DROP TRIGGER sm_check_transfer_copy ON "AppBookCopies";
		DROP FUNCTION sm_check_library_stock_transfer();
		DROP FUNCTION sm_keep_library_transfer_destination();
		DROP FUNCTION sm_check_library_cross_campus_move();
		DROP FUNCTION sm_check_library_transfer_copy() CASCADE;
		DROP FUNCTION sm_check_library_transfer_item() CASCADE;
		DROP FUNCTION sm_check_library_transfer_receipt() CASCADE;
		DROP FUNCTION sm_check_library_transfer_header() CASCADE;
		DROP FUNCTION sm_record_library_transfer_receipt() CASCADE;
		DROP FUNCTION sm_keep_library_transfer_evidence() CASCADE;
		""");
}
