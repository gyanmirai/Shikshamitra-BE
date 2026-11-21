using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class AddLibraryStockReceivingEvidence
{
	private static void InstallReceivingSeal(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		CREATE FUNCTION "LibraryReceiving_CheckSealedReceipt"() RETURNS trigger
		LANGUAGE plpgsql AS $library_receiving_seal$
		DECLARE actual_lines bigint; expected_copies bigint; actual_copies bigint; actual_commands bigint;
		BEGIN
		    SELECT count(*),COALESCE(sum(l."ExpectedCopyCount"),0) INTO actual_lines,expected_copies
		        FROM "AppLibraryStockReceiptLines" l WHERE l."ReceiptId"=NEW."Id" AND l."TenantId"=NEW."TenantId";
		    SELECT count(*) INTO actual_copies FROM "AppLibraryReceivedCopies" c
		        JOIN "AppLibraryStockReceiptLines" l ON l."Id"=c."ReceiptLineId" AND l."TenantId"=c."TenantId"
		        WHERE l."ReceiptId"=NEW."Id" AND l."TenantId"=NEW."TenantId";
		    SELECT count(*) INTO actual_commands FROM "AppLibraryStockReceiptCommands" c
		        WHERE c."ReceiptId"=NEW."Id" AND c."TenantId"=NEW."TenantId";
		    IF actual_lines<>NEW."ExpectedLineCount" OR expected_copies<>NEW."ExpectedCopyCount"
		        OR actual_copies<>NEW."ExpectedCopyCount" OR actual_commands<>1 OR EXISTS (
		            SELECT l."Id" FROM "AppLibraryStockReceiptLines" l LEFT JOIN "AppLibraryReceivedCopies" c
		                ON c."ReceiptLineId"=l."Id" AND c."TenantId"=l."TenantId"
		            WHERE l."ReceiptId"=NEW."Id" AND l."TenantId"=NEW."TenantId"
		            GROUP BY l."Id",l."ExpectedCopyCount" HAVING count(c."Id")<>l."ExpectedCopyCount") THEN
		        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Commit the complete sealed receiving record, copy set and one accepted command together.';
		    END IF;
		    RETURN NULL;
		END;
		$library_receiving_seal$;
		CREATE CONSTRAINT TRIGGER "TR_LibraryReceiving_SealedReceipt" AFTER INSERT ON "AppLibraryStockReceipts"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "LibraryReceiving_CheckSealedReceipt"();
		""");
}
