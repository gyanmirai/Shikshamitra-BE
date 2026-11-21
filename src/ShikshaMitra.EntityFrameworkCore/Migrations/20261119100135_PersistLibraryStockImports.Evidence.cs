using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class PersistLibraryStockImports
{
	private static void AddEvidenceGuards(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		ALTER TABLE "AppLibraryStockImportBatches" ADD CONSTRAINT "CK_AppLibraryStockImportBatch_Counts"
		    CHECK ("RowCount" BETWEEN 1 AND 1000 AND "NewTitleCount" BETWEEN 0 AND "RowCount"
		        AND "Source" BETWEEN 0 AND 3 AND length("PayloadFingerprint") = 64 AND length(trim("IdempotencyKey")) > 0);
		ALTER TABLE "AppLibraryStockImportRows" ADD CONSTRAINT "CK_AppLibraryStockImportRow_Identity"
		    CHECK ("RowNumber" >= 2 AND length(trim("SourceRowId")) > 0 AND length(trim("NormalizedSourceRowId")) > 0
		        AND (NOT "IsNewTitle" OR length(trim("Title")) > 0) AND length(trim("AccessionNumber")) > 0);

		CREATE FUNCTION sm_keep_library_stock_import_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    RAISE EXCEPTION 'Committed library stock import evidence cannot be changed or deleted' USING ERRCODE = '23514';
		END $$;
		CREATE TRIGGER "TR_AppLibraryStockImportBatches_Keep" BEFORE UPDATE OR DELETE ON "AppLibraryStockImportBatches"
		    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_stock_import_evidence();
		CREATE TRIGGER "TR_AppLibraryStockImportRows_Keep" BEFORE UPDATE OR DELETE ON "AppLibraryStockImportRows"
		    FOR EACH ROW EXECUTE FUNCTION sm_keep_library_stock_import_evidence();
		CREATE TRIGGER "TR_AppLibraryStockImportBatches_KeepTruncate" BEFORE TRUNCATE ON "AppLibraryStockImportBatches"
		    FOR EACH STATEMENT EXECUTE FUNCTION sm_keep_library_stock_import_evidence();
		CREATE TRIGGER "TR_AppLibraryStockImportRows_KeepTruncate" BEFORE TRUNCATE ON "AppLibraryStockImportRows"
		    FOR EACH STATEMENT EXECUTE FUNCTION sm_keep_library_stock_import_evidence();

		CREATE FUNCTION sm_check_library_stock_import_batch() RETURNS trigger LANGUAGE plpgsql AS $$
		DECLARE
		    saved_rows integer;
		    new_titles integer;
		BEGIN
		    SELECT count(*)::integer, count(DISTINCT "BookId") FILTER (WHERE "IsNewTitle")
		      INTO saved_rows, new_titles
		      FROM "AppLibraryStockImportRows" WHERE "TenantId" = NEW."TenantId" AND "BatchId" = NEW."Id";
		    IF saved_rows <> NEW."RowCount" OR new_titles <> NEW."NewTitleCount" THEN
		        RAISE EXCEPTION 'The library stock import is missing committed outcomes' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE CONSTRAINT TRIGGER "TR_AppLibraryStockImportBatches_Complete"
		    AFTER INSERT ON "AppLibraryStockImportBatches" DEFERRABLE INITIALLY DEFERRED
		    FOR EACH ROW EXECUTE FUNCTION sm_check_library_stock_import_batch();

		CREATE FUNCTION sm_check_library_stock_import_row() RETURNS trigger LANGUAGE plpgsql AS $$
		BEGIN
		    IF NOT EXISTS (
		        SELECT 1 FROM "AppLibraryStockImportBatches" batch
		        JOIN "AppLibraryStockReceipts" receipt ON receipt."Id" = NEW."ReceiptId" AND receipt."TenantId" = batch."TenantId"
		        JOIN "AppLibraryReceivedCopies" received ON received."CopyId" = NEW."CopyId" AND received."TenantId" = batch."TenantId"
		        JOIN "AppLibraryStockReceiptLines" line ON line."Id" = received."ReceiptLineId" AND line."TenantId" = batch."TenantId"
		        WHERE batch."Id" = NEW."BatchId" AND batch."TenantId" = NEW."TenantId"
		          AND line."ReceiptId" = receipt."Id" AND line."BookId" = NEW."BookId"
		          AND line."SectionId" = batch."SectionId" AND line."ShelfId" = batch."ShelfId"
		          AND received."AccessionNumber" = NEW."AccessionNumber"
		          AND receipt."Source" = batch."Source" AND receipt."SourceName" IS NOT DISTINCT FROM batch."SourceName"
		          AND receipt."RecordedBy" = batch."ImportedBy"
		          AND receipt."Reference" = 'Import ' || replace(batch."Id"::text, '-', '')
		          AND batch."RowCount" = (SELECT count(*) FROM "AppLibraryStockImportRows" outcome
		              WHERE outcome."TenantId" = batch."TenantId" AND outcome."BatchId" = batch."Id")
		    ) THEN
		        RAISE EXCEPTION 'The imported copy does not match the committed stock receipt and import batch' USING ERRCODE = '23514';
		    END IF;
		    RETURN NEW;
		END $$;
		CREATE CONSTRAINT TRIGGER "TR_AppLibraryStockImportRows_Receipt"
		    AFTER INSERT ON "AppLibraryStockImportRows" DEFERRABLE INITIALLY DEFERRED
		    FOR EACH ROW EXECUTE FUNCTION sm_check_library_stock_import_row();
		""");

	private static void PreventEvidenceLoss(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		LOCK TABLE "AppLibraryStockImportBatches", "AppLibraryStockImportRows" IN ACCESS EXCLUSIVE MODE;
		DO $$ BEGIN
		    IF EXISTS (SELECT 1 FROM "AppLibraryStockImportBatches")
		        OR EXISTS (SELECT 1 FROM "AppLibraryStockImportRows") THEN
		        RAISE EXCEPTION 'Restore a verified backup or apply a forward migration to preserve accepted library stock imports'
		            USING ERRCODE = '23514';
		    END IF;
		END $$;
		""");
}
