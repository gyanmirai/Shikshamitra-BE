using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class PersistLibraryClassroomCustody
{
	private static void AddCustodyEvidenceGuards(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		LOCK TABLE "AppBookCopies", "AppLibraryCopyEvents" IN SHARE ROW EXCLUSIVE MODE;
		DO $$ BEGIN
		    IF EXISTS (SELECT 1 FROM "AppBookCopies" WHERE "Status" = 7)
		        OR EXISTS (SELECT 1 FROM "AppLibraryCopyEvents" WHERE "EventType" IN (9, 10)) THEN
		        RAISE EXCEPTION 'Review pre-existing values reserved for classroom custody before applying this migration' USING ERRCODE = '23514';
		    END IF;
		END $$;
		ALTER TABLE "AppLibraryCustodyHandoffs" ADD CONSTRAINT "CK_AppLibraryCustodyHandoff_Details"
		    CHECK ("CopyCount" BETWEEN 1 AND 100 AND length(trim("Purpose")) > 0 AND length(trim("Custodian")) > 0
		        AND ("ExpectedReturnOn" IS NULL OR "ExpectedReturnOn" >= "HandedOffOn") AND length("PayloadFingerprint") = 64);
		ALTER TABLE "AppLibraryCustodyReceipts" ADD CONSTRAINT "CK_AppLibraryCustodyReceipt_Details"
		    CHECK ("CopyCount" BETWEEN 1 AND 100 AND length("PayloadFingerprint") = 64);
		ALTER TABLE "AppLibraryCustodyItems" ADD CONSTRAINT "CK_AppLibraryCustodyItem_Return"
		    CHECK (("ReturnReceiptId" IS NULL AND "ReturnEventId" IS NULL AND "ReturnedCondition" IS NULL)
		        OR ("ReturnReceiptId" IS NOT NULL AND "ReturnEventId" IS NOT NULL
		            AND "ReturnedCondition" IS NOT NULL AND "ReturnedCondition" IN (0, 4, 5)));

		CREATE FUNCTION sm_keep_library_custody_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    RAISE EXCEPTION 'Classroom custody evidence cannot be rewritten or removed' USING ERRCODE = '23514';
		END $$;
		CREATE TRIGGER sm_keep_custody_handoff BEFORE UPDATE OR DELETE ON "AppLibraryCustodyHandoffs"
		    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_custody_evidence();
		CREATE TRIGGER sm_keep_custody_receipt BEFORE UPDATE OR DELETE ON "AppLibraryCustodyReceipts"
		    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_custody_evidence();
		CREATE TRIGGER sm_keep_custody_item BEFORE DELETE ON "AppLibraryCustodyItems"
		    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_custody_evidence();
		CREATE TRIGGER sm_keep_custody_handoff_truncate BEFORE TRUNCATE ON "AppLibraryCustodyHandoffs"
		    FOR EACH STATEMENT EXECUTE FUNCTION sm_keep_library_custody_evidence();
		CREATE TRIGGER sm_keep_custody_receipt_truncate BEFORE TRUNCATE ON "AppLibraryCustodyReceipts"
		    FOR EACH STATEMENT EXECUTE FUNCTION sm_keep_library_custody_evidence();
		CREATE TRIGGER sm_keep_custody_item_truncate BEFORE TRUNCATE ON "AppLibraryCustodyItems"
		    FOR EACH STATEMENT EXECUTE FUNCTION sm_keep_library_custody_evidence();

		CREATE FUNCTION sm_record_library_custody_return() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    IF OLD."ReturnReceiptId" IS NOT NULL OR NEW."ReturnReceiptId" IS NULL OR
		        (to_jsonb(NEW) - 'ReturnReceiptId' - 'ReturnEventId' - 'ReturnedCondition') IS DISTINCT FROM
		        (to_jsonb(OLD) - 'ReturnReceiptId' - 'ReturnEventId' - 'ReturnedCondition') THEN
		        RAISE EXCEPTION 'A custody copy can be received once and keeps its original handoff' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE TRIGGER sm_record_custody_return BEFORE UPDATE ON "AppLibraryCustodyItems"
		    FOR EACH ROW EXECUTE FUNCTION sm_record_library_custody_return();

		CREATE FUNCTION sm_check_library_custody_handoff() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    IF NEW."CopyCount" <> (SELECT count(*) FROM "AppLibraryCustodyItems" i
		        WHERE i."HandoffId" = NEW."Id" AND i."TenantId" = NEW."TenantId") THEN
		        RAISE EXCEPTION 'A classroom handoff must retain every assigned copy' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE CONSTRAINT TRIGGER sm_check_custody_handoff AFTER INSERT ON "AppLibraryCustodyHandoffs"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sm_check_library_custody_handoff();

		CREATE FUNCTION sm_check_library_custody_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    IF NEW."CopyCount" <> (SELECT count(*) FROM "AppLibraryCustodyItems" i
		        WHERE i."ReturnReceiptId" = NEW."Id" AND i."HandoffId" = NEW."HandoffId" AND i."TenantId" = NEW."TenantId") THEN
		        RAISE EXCEPTION 'A custody receipt must retain every received copy' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE CONSTRAINT TRIGGER sm_check_custody_receipt AFTER INSERT ON "AppLibraryCustodyReceipts"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sm_check_library_custody_receipt();

		CREATE FUNCTION sm_check_library_custody_item() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    IF NOT EXISTS (SELECT 1 FROM "AppLibraryCustodyHandoffs" h
		        JOIN "AppLibraryCopyEvents" e ON e."Id" = NEW."HandoffEventId" AND e."TenantId" = h."TenantId"
		        WHERE h."Id" = NEW."HandoffId" AND h."TenantId" = NEW."TenantId"
		          AND h."CopyCount" = (SELECT count(*) FROM "AppLibraryCustodyItems" i WHERE i."HandoffId" = h."Id" AND i."TenantId" = h."TenantId")
		          AND e."CopyId" = NEW."CopyId" AND e."BookId" = NEW."BookId" AND e."AccessionNumber" = NEW."AccessionNumber"
		          AND e."EventType" = 9 AND e."PreviousStatus" = 0 AND e."NextStatus" = 7 AND e."ActorId" = h."ActorId"
		          AND e."FromSectionId" = NEW."HomeSectionId" AND e."FromShelfId" = NEW."HomeShelfId"
		          AND e."ToSectionId" = NEW."HomeSectionId" AND e."ToShelfId" = NEW."HomeShelfId") THEN
		        RAISE EXCEPTION 'A custody item must match its original copy handoff event' USING ERRCODE = '23514';
		    END IF;
		    IF NEW."ReturnReceiptId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "AppLibraryCustodyReceipts" r
		        JOIN "AppLibraryCopyEvents" e ON e."Id" = NEW."ReturnEventId" AND e."TenantId" = r."TenantId"
		        JOIN "AppLibraryCustodyHandoffs" h ON h."Id" = r."HandoffId" AND h."TenantId" = r."TenantId"
		        WHERE r."Id" = NEW."ReturnReceiptId" AND h."Id" = NEW."HandoffId" AND h."TenantId" = NEW."TenantId"
		          AND r."ReceivedOn" >= h."HandedOffOn" AND r."ActorId" = e."ActorId"
		          AND r."CopyCount" = (SELECT count(*) FROM "AppLibraryCustodyItems" i WHERE i."ReturnReceiptId" = r."Id" AND i."TenantId" = r."TenantId")
		          AND e."CopyId" = NEW."CopyId" AND e."BookId" = NEW."BookId" AND e."AccessionNumber" = NEW."AccessionNumber"
		          AND e."EventType" = 10 AND e."PreviousStatus" = 7 AND e."NextStatus" = NEW."ReturnedCondition"
		          AND e."FromSectionId" = NEW."HomeSectionId" AND e."FromShelfId" = NEW."HomeShelfId"
		          AND e."ToSectionId" = NEW."HomeSectionId" AND e."ToShelfId" = NEW."HomeShelfId") THEN
		        RAISE EXCEPTION 'A custody receipt must match the returned copy and recorded condition' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE CONSTRAINT TRIGGER sm_check_custody_item AFTER INSERT OR UPDATE ON "AppLibraryCustodyItems"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sm_check_library_custody_item();

		CREATE FUNCTION sm_check_library_custody_copy() RETURNS trigger LANGUAGE plpgsql AS $$
		DECLARE copy_id uuid;
		BEGIN
		    IF TG_TABLE_NAME = 'AppBookCopies' THEN copy_id := NEW."Id";
		    ELSE copy_id := NEW."CopyId"; END IF;
		    IF NOT EXISTS (SELECT 1 FROM "AppBookCopies" c LEFT JOIN "AppLibraryCustodyItems" i
		        ON i."CopyId" = c."Id" AND i."TenantId" = c."TenantId" AND i."ReturnReceiptId" IS NULL
		        WHERE c."Id" = copy_id AND ((c."Status" <> 7 AND i."Id" IS NULL) OR
		            (c."Status" = 7 AND i."Id" IS NOT NULL AND c."BookId" = i."BookId" AND c."AccessionNumber" = i."AccessionNumber"
		             AND c."SectionId" = i."HomeSectionId" AND c."ShelfId" = i."HomeShelfId"))) THEN
		        RAISE EXCEPTION 'The copy register must agree with the active classroom custody record' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE CONSTRAINT TRIGGER sm_new_copy_custody AFTER INSERT ON "AppBookCopies" DEFERRABLE INITIALLY DEFERRED
		    FOR EACH ROW WHEN (NEW."Status" = 7) EXECUTE FUNCTION sm_check_library_custody_copy();
		CREATE CONSTRAINT TRIGGER sm_update_copy_custody AFTER UPDATE ON "AppBookCopies" DEFERRABLE INITIALLY DEFERRED
		    FOR EACH ROW WHEN (OLD."Status" = 7 OR NEW."Status" = 7) EXECUTE FUNCTION sm_check_library_custody_copy();
		CREATE CONSTRAINT TRIGGER sm_item_copy_custody AFTER INSERT OR UPDATE ON "AppLibraryCustodyItems"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sm_check_library_custody_copy();

		CREATE FUNCTION sm_check_library_stock_custody() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    IF NEW."Source" <> 1 OR NEW."CurrentStatus" IS DISTINCT FROM 7 OR NOT EXISTS (
		        SELECT 1 FROM "AppLibraryCustodyItems" i JOIN "AppBookCopies" c ON c."Id" = i."CopyId" AND c."TenantId" = i."TenantId"
		        WHERE i."TenantId" = NEW."TenantId" AND i."CopyId" = NEW."RowId" AND i."ReturnReceiptId" IS NULL
		          AND i."AccessionNumber" = NEW."AccessionNumber" AND c."Status" = 7) THEN
		        RAISE EXCEPTION 'A published custody finding needs matching active custody evidence' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE TRIGGER sm_check_stock_custody BEFORE INSERT ON "AppLibraryStockVerificationResults"
		    FOR EACH ROW WHEN (NEW."Finding" = 16) EXECUTE FUNCTION sm_check_library_stock_custody();
		""");

	private static void RemoveCustodyCopyGuards(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		LOCK TABLE "AppBookCopies", "AppLibraryCopyEvents", "AppLibraryCustodyHandoffs", "AppLibraryCustodyReceipts", "AppLibraryCustodyItems" IN ACCESS EXCLUSIVE MODE;
		DO $$ BEGIN
		    IF EXISTS (SELECT 1 FROM "AppLibraryCustodyHandoffs") OR EXISTS (SELECT 1 FROM "AppLibraryCustodyReceipts")
		        OR EXISTS (SELECT 1 FROM "AppLibraryCustodyItems") OR EXISTS (SELECT 1 FROM "AppBookCopies" WHERE "Status" = 7)
		        OR EXISTS (SELECT 1 FROM "AppLibraryCopyEvents" WHERE "EventType" IN (9, 10)) THEN
		        RAISE EXCEPTION 'Preserve classroom custody evidence with a forward migration or verified backup' USING ERRCODE = '23514';
		    END IF;
		END $$;
		DROP TRIGGER sm_new_copy_custody ON "AppBookCopies";
		DROP TRIGGER sm_update_copy_custody ON "AppBookCopies";
		DROP TRIGGER sm_check_stock_custody ON "AppLibraryStockVerificationResults";
		""");

	private static void RemoveCustodyEvidenceFunctions(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		DROP FUNCTION sm_check_library_custody_copy();
		DROP FUNCTION sm_check_library_stock_custody();
		DROP FUNCTION sm_check_library_custody_item();
		DROP FUNCTION sm_check_library_custody_receipt();
		DROP FUNCTION sm_check_library_custody_handoff();
		DROP FUNCTION sm_record_library_custody_return();
		DROP FUNCTION sm_keep_library_custody_evidence();
		""");
}
