using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class CompleteLibraryTypedSettlementAndRecordedStaffScope
{
	private static readonly string[] TypedSettlementImmutableTables =
	[
		"AppLibraryBorrowerScopeCaptures", "AppLibraryBorrowerAssignmentSnapshots", "Finance_ExternalSettlementPolicies",
		"Finance_ChargeWaiverReversals", "Finance_ChargeWaiverApplications"
	];

	private static void InstallTypedSettlementEvidenceGuards(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			CREATE FUNCTION "LibraryTypedSettlement_RejectMutation"() RETURNS trigger
			LANGUAGE plpgsql AS $library_typed_evidence$
			BEGIN
			    RAISE EXCEPTION USING ERRCODE='P0001', MESSAGE='Library typed settlement and recorded scope evidence is append-only; record a new authorized event.';
			END;
			$library_typed_evidence$;
			""");
		foreach (var table in TypedSettlementImmutableTables)
			migrationBuilder.Sql($$"""
				CREATE TRIGGER "TR_LibraryTypedEvidence_NoUpdateDelete" BEFORE UPDATE OR DELETE ON "{{table}}"
				    FOR EACH ROW EXECUTE FUNCTION "LibraryTypedSettlement_RejectMutation"();
				CREATE TRIGGER "TR_LibraryTypedEvidence_NoTruncate" BEFORE TRUNCATE ON "{{table}}"
				    FOR EACH STATEMENT EXECUTE FUNCTION "LibraryTypedSettlement_RejectMutation"();
				""");
		InstallRecordedAssignmentSeal(migrationBuilder);
		InstallWaiverRequestEvidenceGuard(migrationBuilder);
	}

	private static void RemoveTypedSettlementEvidenceGuards(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			DROP TRIGGER "TR_FinanceWaiverRequest_Evidence" ON "Finance_ExternalWaiverRequests";
			DROP TRIGGER "TR_FinanceWaiverRequest_NoTruncate" ON "Finance_ExternalWaiverRequests";
			DROP FUNCTION "FinanceWaiverRequest_RetainEvidence"();
			DROP TRIGGER "TR_LibraryBorrowerScope_SealedParent" ON "AppLibraryBorrowerScopeCaptures";
			DROP TRIGGER "TR_LibraryBorrowerScope_SealedChild" ON "AppLibraryBorrowerAssignmentSnapshots";
			DROP FUNCTION "LibraryBorrowerScope_CheckSealedSet"();
			""");
		foreach (var table in TypedSettlementImmutableTables)
			migrationBuilder.Sql($$"""
				DROP TRIGGER "TR_LibraryTypedEvidence_NoUpdateDelete" ON "{{table}}";
				DROP TRIGGER "TR_LibraryTypedEvidence_NoTruncate" ON "{{table}}";
				""");
		migrationBuilder.Sql("DROP FUNCTION \"LibraryTypedSettlement_RejectMutation\"();");
	}
}
