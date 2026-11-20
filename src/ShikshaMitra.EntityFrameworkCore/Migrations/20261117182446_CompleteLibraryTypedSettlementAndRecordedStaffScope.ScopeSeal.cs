using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class CompleteLibraryTypedSettlementAndRecordedStaffScope
{
	private static void InstallRecordedAssignmentSeal(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		CREATE FUNCTION "LibraryBorrowerScope_CheckSealedSet"() RETURNS trigger
		LANGUAGE plpgsql AS $library_scope_seal$
		DECLARE scope_tenant uuid; scope_id uuid; expected_count integer; actual_count bigint;
		BEGIN
		    scope_tenant := NEW."TenantId";
		    IF TG_TABLE_NAME='AppLibraryBorrowerScopeCaptures' THEN scope_id:=NEW."Id";
		    ELSE scope_id:=NEW."CaptureId"; END IF;
		    SELECT "ExpectedAssignmentCount" INTO expected_count FROM "AppLibraryBorrowerScopeCaptures"
		        WHERE "TenantId"=scope_tenant AND "Id"=scope_id FOR NO KEY UPDATE;
		    IF NOT FOUND THEN RAISE EXCEPTION 'Library sealed assignment set owner is missing.'; END IF;
		    SELECT count(*) INTO actual_count FROM "AppLibraryBorrowerAssignmentSnapshots"
		        WHERE "TenantId"=scope_tenant AND "CaptureId"=scope_id;
		    IF actual_count<>expected_count THEN
		        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='Library sealed assignment set must retain exactly its originally recorded children.';
		    END IF;
		    RETURN NULL;
		END;
		$library_scope_seal$;
		CREATE CONSTRAINT TRIGGER "TR_LibraryBorrowerScope_SealedParent" AFTER INSERT ON "AppLibraryBorrowerScopeCaptures"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "LibraryBorrowerScope_CheckSealedSet"();
		CREATE CONSTRAINT TRIGGER "TR_LibraryBorrowerScope_SealedChild" AFTER INSERT ON "AppLibraryBorrowerAssignmentSnapshots"
		    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "LibraryBorrowerScope_CheckSealedSet"();
		""");
}
