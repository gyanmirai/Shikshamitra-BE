using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class AddLibraryStockReceivingEvidence
{
	private static void InstallReceivingSourceGuards(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		-- Database-only guard metadata, not an acquisition fact or an ORM-supplied value.
		-- Full transaction IDs survive xid wraparound and remain stable across EF savepoints.
		ALTER TABLE "AppLibraryStockReceipts" ADD COLUMN "RecordingTransactionId" xid8 NOT NULL DEFAULT pg_current_xact_id();
		CREATE FUNCTION "LibraryReceiving_CheckNewEvidence"() RETURNS trigger
		LANGUAGE plpgsql AS $library_receiving_new$
		DECLARE owner_birth xid8; owner_version xid; owner_actor uuid; owner_time timestamptz; maximum_lines integer;
		    source_book uuid; source_section uuid; source_shelf uuid;
		BEGIN
		    IF NEW."TenantId" IS NULL OR NEW."TenantId"='00000000-0000-0000-0000-000000000000' THEN
		        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Stock receiving evidence requires an actual school.';
		    END IF;
		    IF TG_TABLE_NAME='AppLibraryStockReceipts' THEN
		        NEW."RecordingTransactionId" := pg_current_xact_id();
		        IF NEW."RecordedBy"='00000000-0000-0000-0000-000000000000' OR NOT isfinite(NEW."RecordedAt")
		            OR NOT isfinite(NEW."ReceivedOn") OR NEW."ReceivedOn"<='0001-01-01'::date THEN
		            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Stock receiving requires its actual actor, receipt date and recorded instant.';
		        END IF;
		    ELSIF TG_TABLE_NAME='AppLibraryStockReceiptLines' THEN
		        SELECT h."RecordingTransactionId",h."ExpectedLineCount" INTO owner_birth,maximum_lines FROM "AppLibraryStockReceipts" h
		            WHERE h."Id"=NEW."ReceiptId" AND h."TenantId"=NEW."TenantId";
		        IF NOT FOUND OR owner_birth IS DISTINCT FROM pg_current_xact_id() THEN
		            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Receiving lines must be saved with their new receipt.';
		        END IF;
		        IF NEW."LineNumber">maximum_lines OR NEW."Currency" IS NOT NULL AND NEW."Currency" !~ '^[A-Z]{3}$' THEN
		            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Receiving line ordinal or currency evidence is invalid.';
		        END IF;
		        PERFORM 1 FROM "AppBooks" b JOIN "AppLibrarySections" s ON s."Id"=NEW."SectionId"
		            JOIN "AppLibraryShelves" sh ON sh."Id"=NEW."ShelfId" AND sh."SectionId"=s."Id"
		            WHERE b."Id"=NEW."BookId" AND b."TenantId"=NEW."TenantId" AND s."TenantId"=NEW."TenantId";
		        IF NOT FOUND THEN
		            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Receiving title and shelf must belong to the recorded school and section.';
		        END IF;
		    ELSIF TG_TABLE_NAME='AppLibraryReceivedCopies' THEN
		        SELECT h."RecordingTransactionId",h.xmin,l."BookId",l."SectionId",l."ShelfId" INTO owner_birth,owner_version,source_book,source_section,source_shelf
		            FROM "AppLibraryStockReceiptLines" l JOIN "AppLibraryStockReceipts" h
		                ON h."Id"=l."ReceiptId" AND h."TenantId"=l."TenantId"
		            WHERE l."Id"=NEW."ReceiptLineId" AND l."TenantId"=NEW."TenantId";
		        IF NOT FOUND OR owner_birth IS DISTINCT FROM pg_current_xact_id() THEN
		            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Received copy links cannot be appended to a committed receipt.';
		        END IF;
		        PERFORM 1 FROM "AppBookCopies" c WHERE c."Id"=NEW."CopyId" AND c."TenantId"=NEW."TenantId"
		            AND c."BookId"=source_book AND c."SectionId"=source_section AND c."ShelfId"=source_shelf
		            AND c."AccessionNumber"=NEW."AccessionNumber" AND c."CopyNumber"=NEW."CopyNumber"
		            AND c."CopyNumber">0 AND c."Status"=0 AND c.xmin=owner_version;
		        IF NOT FOUND THEN
		            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Receiving copy evidence does not match the school copy version and initial location in this transaction.';
		        END IF;
		    ELSIF TG_TABLE_NAME='AppLibraryStockReceiptCommands' THEN
		        SELECT h."RecordingTransactionId",h."RecordedBy",h."RecordedAt" INTO owner_birth,owner_actor,owner_time
		            FROM "AppLibraryStockReceipts" h WHERE h."Id"=NEW."ReceiptId" AND h."TenantId"=NEW."TenantId";
		        IF NOT FOUND OR owner_birth IS DISTINCT FROM pg_current_xact_id() OR NEW."ActorId"<>owner_actor
		            OR NEW."RecordedAt"<>owner_time OR NEW."PayloadFingerprint" !~ '^[A-Fa-f0-9]{64}$'
		            OR length(NEW."IdempotencyKey") NOT BETWEEN 1 AND 100 OR NEW."IdempotencyKey"<>btrim(NEW."IdempotencyKey")
		            OR NEW."IdempotencyKey" ~ '[[:cntrl:]]' THEN
		            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='The accepted receiving command must retain its original school, actor, retry key and payload evidence.';
		        END IF;
		    END IF;
		    RETURN NEW;
		END;
		$library_receiving_new$;
		CREATE TRIGGER "TR_LibraryReceiving_NewHeader" BEFORE INSERT ON "AppLibraryStockReceipts"
		    FOR EACH ROW EXECUTE FUNCTION "LibraryReceiving_CheckNewEvidence"();
		CREATE TRIGGER "TR_LibraryReceiving_NewLine" BEFORE INSERT ON "AppLibraryStockReceiptLines"
		    FOR EACH ROW EXECUTE FUNCTION "LibraryReceiving_CheckNewEvidence"();
		CREATE TRIGGER "TR_LibraryReceiving_NewCopyLink" BEFORE INSERT ON "AppLibraryReceivedCopies"
		    FOR EACH ROW EXECUTE FUNCTION "LibraryReceiving_CheckNewEvidence"();
		CREATE TRIGGER "TR_LibraryReceiving_NewCommand" BEFORE INSERT ON "AppLibraryStockReceiptCommands"
		    FOR EACH ROW EXECUTE FUNCTION "LibraryReceiving_CheckNewEvidence"();
		""");
}
