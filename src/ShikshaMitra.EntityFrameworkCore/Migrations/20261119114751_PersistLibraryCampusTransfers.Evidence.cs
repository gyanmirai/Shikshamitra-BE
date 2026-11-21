using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class PersistLibraryCampusTransfers
{
	private static void AddTransferEvidenceGuards(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		LOCK TABLE "AppBookCopies", "AppLibraryCopyEvents" IN SHARE ROW EXCLUSIVE MODE;
		DO $$ BEGIN
		    IF EXISTS (SELECT 1 FROM "AppBookCopies" WHERE "Status" = 8)
		        OR EXISTS (SELECT 1 FROM "AppLibraryCopyEvents" WHERE "EventType" IN (11, 12)) THEN
		        RAISE EXCEPTION 'Review pre-existing copy values reserved for campus transfers' USING ERRCODE = '23514';
		    END IF;
		END $$;
		ALTER TABLE "AppLibraryCampusTransfers" ADD CONSTRAINT "CK_LibraryCampusTransfer_Details"
		    CHECK ("CopyCount" BETWEEN 1 AND 100 AND "SourceCampusId" <> "DestinationCampusId" AND
		        "SourceSectionId" <> "DestinationSectionId" AND length(trim("Reason")) > 0 AND length("PayloadFingerprint") = 64);
		ALTER TABLE "AppLibraryCampusTransferReceipts" ADD CONSTRAINT "CK_LibraryCampusReceipt_Details"
		    CHECK ("CopyCount" BETWEEN 1 AND 100 AND length("PayloadFingerprint") = 64);
		ALTER TABLE "AppLibraryCampusTransferItems" ADD CONSTRAINT "CK_LibraryCampusItem_Receipt"
		    CHECK (("ReceiptId" IS NULL AND "ReceiptEventId" IS NULL AND "Condition" IS NULL) OR
		        ("ReceiptId" IS NOT NULL AND "ReceiptEventId" IS NOT NULL AND "Condition" IS NOT NULL AND "Condition" IN (0, 4, 5)));

		CREATE FUNCTION sm_keep_library_transfer_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    RAISE EXCEPTION 'Campus transfer evidence must be preserved' USING ERRCODE = '23514';
		END $$;
		CREATE TRIGGER sm_keep_transfer_header BEFORE UPDATE OR DELETE ON "AppLibraryCampusTransfers"
		    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_transfer_evidence();
		CREATE TRIGGER sm_keep_transfer_receipt BEFORE UPDATE OR DELETE ON "AppLibraryCampusTransferReceipts"
		    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_transfer_evidence();
		CREATE TRIGGER sm_keep_transfer_item BEFORE DELETE ON "AppLibraryCampusTransferItems"
		    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_transfer_evidence();
		CREATE TRIGGER sm_keep_transfer_header_truncate BEFORE TRUNCATE ON "AppLibraryCampusTransfers"
		    FOR EACH STATEMENT EXECUTE FUNCTION sm_keep_library_transfer_evidence();
		CREATE TRIGGER sm_keep_transfer_receipt_truncate BEFORE TRUNCATE ON "AppLibraryCampusTransferReceipts"
		    FOR EACH STATEMENT EXECUTE FUNCTION sm_keep_library_transfer_evidence();
		CREATE TRIGGER sm_keep_transfer_item_truncate BEFORE TRUNCATE ON "AppLibraryCampusTransferItems"
		    FOR EACH STATEMENT EXECUTE FUNCTION sm_keep_library_transfer_evidence();

		CREATE FUNCTION sm_record_library_transfer_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    IF OLD."ReceiptId" IS NOT NULL OR NEW."ReceiptId" IS NULL OR
		        (to_jsonb(NEW) - 'ReceiptId' - 'ReceiptEventId' - 'Condition') IS DISTINCT FROM
		        (to_jsonb(OLD) - 'ReceiptId' - 'ReceiptEventId' - 'Condition') THEN
		        RAISE EXCEPTION 'An accession can be received once on its transfer' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE TRIGGER sm_record_transfer_receipt BEFORE UPDATE ON "AppLibraryCampusTransferItems"
		    FOR EACH ROW EXECUTE FUNCTION sm_record_library_transfer_receipt();

		CREATE FUNCTION sm_check_library_transfer_header() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    PERFORM 1 FROM "AppLibrarySections" s
		        JOIN "AppLibrarySections" d ON d."Id" = NEW."DestinationSectionId" AND d."TenantId" = NEW."TenantId"
		        JOIN "AppLibraryShelves" ds ON ds."Id" = NEW."DestinationShelfId" AND ds."SectionId" = d."Id"
		        JOIN "AppAdmissionCampuses" sc ON sc."Id" = s."CampusId" AND sc."TenantId" = NEW."TenantId"
		        JOIN "AppAdmissionCampuses" dc ON dc."Id" = d."CampusId" AND dc."TenantId" = NEW."TenantId"
		        WHERE s."Id" = NEW."SourceSectionId" AND s."TenantId" = NEW."TenantId" AND sc."Id" = NEW."SourceCampusId"
		          AND dc."Id" = NEW."DestinationCampusId" AND sc."IsActive" AND NOT sc."IsDeleted"
		          AND dc."IsActive" AND NOT dc."IsDeleted" FOR SHARE OF s, d, ds, sc, dc;
		    IF NOT FOUND OR NEW."CopyCount" <> (SELECT count(*) FROM "AppLibraryCampusTransferItems" i
		        WHERE i."TransferId" = NEW."Id" AND i."TenantId" = NEW."TenantId") THEN
		        RAISE EXCEPTION 'A campus transfer must retain its actual route and every dispatched accession' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE CONSTRAINT TRIGGER sm_check_transfer_header AFTER INSERT ON "AppLibraryCampusTransfers"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sm_check_library_transfer_header();

		CREATE FUNCTION sm_check_library_transfer_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    IF NEW."CopyCount" <> (SELECT count(*) FROM "AppLibraryCampusTransferItems" i
		        WHERE i."ReceiptId" = NEW."Id" AND i."TransferId" = NEW."TransferId" AND i."TenantId" = NEW."TenantId") THEN
		        RAISE EXCEPTION 'A transfer receipt must retain every received accession' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE CONSTRAINT TRIGGER sm_check_transfer_receipt AFTER INSERT ON "AppLibraryCampusTransferReceipts"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sm_check_library_transfer_receipt();

		CREATE FUNCTION sm_check_library_transfer_item() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    IF NOT EXISTS (SELECT 1 FROM "AppLibraryCampusTransfers" h
		        JOIN "AppLibraryCopyEvents" e ON e."Id" = NEW."DispatchEventId" AND e."TenantId" = h."TenantId"
		        WHERE h."Id" = NEW."TransferId" AND h."TenantId" = NEW."TenantId"
		          AND h."CopyCount" = (SELECT count(*) FROM "AppLibraryCampusTransferItems" i WHERE i."TransferId" = h."Id" AND i."TenantId" = h."TenantId")
		          AND e."EventType" = 11 AND e."PreviousStatus" = 0 AND e."NextStatus" = 8 AND e."ActorId" = h."ActorId"
		          AND e."CopyId" = NEW."CopyId" AND e."BookId" = NEW."BookId" AND e."AccessionNumber" = NEW."AccessionNumber"
		          AND e."FromSectionId" = h."SourceSectionId" AND e."ToSectionId" = h."SourceSectionId"
		          AND e."FromShelfId" = NEW."SourceShelfId" AND e."ToShelfId" = NEW."SourceShelfId") THEN
		        RAISE EXCEPTION 'A transfer item must match its original dispatch event' USING ERRCODE = '23514';
		    END IF;
		    IF NEW."ReceiptId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "AppLibraryCampusTransferReceipts" r
		        JOIN "AppLibraryCampusTransfers" h ON h."Id" = r."TransferId" AND h."TenantId" = r."TenantId"
		        JOIN "AppLibraryCopyEvents" e ON e."Id" = NEW."ReceiptEventId" AND e."TenantId" = r."TenantId"
		        WHERE r."Id" = NEW."ReceiptId" AND h."Id" = NEW."TransferId" AND h."TenantId" = NEW."TenantId"
		          AND r."ReceivedOn" >= h."DispatchedOn" AND e."ActorId" = r."ActorId"
		          AND r."CopyCount" = (SELECT count(*) FROM "AppLibraryCampusTransferItems" i WHERE i."ReceiptId" = r."Id" AND i."TenantId" = r."TenantId")
		          AND e."EventType" = 12 AND e."PreviousStatus" = 8 AND e."NextStatus" = NEW."Condition"
		          AND e."CopyId" = NEW."CopyId" AND e."BookId" = NEW."BookId" AND e."AccessionNumber" = NEW."AccessionNumber"
		          AND e."FromSectionId" = h."SourceSectionId" AND e."FromShelfId" = NEW."SourceShelfId"
		          AND e."ToSectionId" = h."DestinationSectionId" AND e."ToShelfId" = h."DestinationShelfId") THEN
		        RAISE EXCEPTION 'A transfer receipt must match the recorded destination, accession and condition' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE CONSTRAINT TRIGGER sm_check_transfer_item AFTER INSERT OR UPDATE ON "AppLibraryCampusTransferItems"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sm_check_library_transfer_item();
		""");
}
