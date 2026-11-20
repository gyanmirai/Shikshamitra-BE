using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class AddLibraryStockReceivingEvidence
{
	private static void InstallReceivingRetention(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			CREATE FUNCTION "LibraryReceiving_RejectMutation"() RETURNS trigger
			LANGUAGE plpgsql AS $library_receiving_immutable$
			BEGIN
			    RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Committed stock receiving evidence cannot be rewritten, removed or truncated.';
			END;
			$library_receiving_immutable$;
			""");
		foreach (var table in new[] { "AppLibraryStockReceipts", "AppLibraryStockReceiptLines", "AppLibraryReceivedCopies", "AppLibraryStockReceiptCommands" })
			migrationBuilder.Sql($"""
				CREATE TRIGGER "TR_LibraryReceiving_Immutable" BEFORE UPDATE OR DELETE ON "{table}"
				    FOR EACH ROW EXECUTE FUNCTION "LibraryReceiving_RejectMutation"();
				CREATE TRIGGER "TR_LibraryReceiving_NoTruncate" BEFORE TRUNCATE ON "{table}"
				    FOR EACH STATEMENT EXECUTE FUNCTION "LibraryReceiving_RejectMutation"();
				""");
	}

	private static void EnsureReceivingRollbackSafe(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		LOCK TABLE "AppLibraryStockReceipts","AppLibraryStockReceiptLines","AppLibraryReceivedCopies","AppLibraryStockReceiptCommands"
		    IN ACCESS EXCLUSIVE MODE;
		DO $library_receiving_down$
		BEGIN
		    IF EXISTS (SELECT 1 FROM "AppLibraryStockReceipts") OR EXISTS (SELECT 1 FROM "AppLibraryStockReceiptLines")
		        OR EXISTS (SELECT 1 FROM "AppLibraryReceivedCopies") OR EXISTS (SELECT 1 FROM "AppLibraryStockReceiptCommands") THEN
		        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Receiving rollback would erase acquisition provenance. Retain the evidence or use a reviewed forward migration.';
		    END IF;
		END;
		$library_receiving_down$;
		""");

	private static void RemoveReceivingGuardFunctions(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		DROP FUNCTION "LibraryReceiving_CheckNewEvidence"();
		DROP FUNCTION "LibraryReceiving_CheckSealedReceipt"();
		DROP FUNCTION "LibraryReceiving_RejectMutation"();
		""");
}
