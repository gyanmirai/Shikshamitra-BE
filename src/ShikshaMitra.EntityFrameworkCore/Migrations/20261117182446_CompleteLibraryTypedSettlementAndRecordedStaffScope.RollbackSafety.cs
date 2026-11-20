using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class CompleteLibraryTypedSettlementAndRecordedStaffScope
{
	private static void RefuseLossyTypedSettlementRollback(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("""
			LOCK TABLE "AppLibraryBorrowerScopeCaptures", "AppLibraryBorrowerAssignmentSnapshots",
			    "Finance_ExternalSettlementPolicies", "Finance_ExternalWaiverRequests", "Finance_ChargeWaiverReversals",
			    "Finance_FeeRecordMetadatas", "Finance_ChargeCollectionRecords", "Finance_FeePaymentReceipts",
			    "Finance_ChargeWaiverApplications", "Finance_PaymentMethodRecords", "Finance_FeeRefundRequests",
			    "Finance_CashCollections", "Finance_Transactions", "Finance_ExternalReceivableCommands",
			    "AppBookIssues", "AppBookReservations", "AppLibraryLoanEvents", "AppLibraryReservationEvents"
			    IN ACCESS EXCLUSIVE MODE;
			DO $library_typed_rollback$
			BEGIN
			    IF EXISTS (SELECT 1 FROM "AppLibraryBorrowerScopeCaptures") OR
			       EXISTS (SELECT 1 FROM "AppLibraryBorrowerAssignmentSnapshots") OR
			       EXISTS (SELECT 1 FROM "Finance_ExternalSettlementPolicies") OR
			       EXISTS (SELECT 1 FROM "Finance_ExternalWaiverRequests") OR
			       EXISTS (SELECT 1 FROM "Finance_ChargeWaiverReversals") OR
			       EXISTS (SELECT 1 FROM "Finance_FeeRecordMetadatas" WHERE "EmployeeId" IS NOT NULL OR "Currency" IS NOT NULL) OR
			       EXISTS (SELECT 1 FROM "Finance_ChargeCollectionRecords" WHERE "EmployeeId" IS NOT NULL OR "StudentId" IS NULL) OR
			       EXISTS (SELECT 1 FROM "Finance_FeePaymentReceipts" WHERE "EmployeeId" IS NOT NULL OR "Currency" IS NOT NULL OR
			           "BoardId" IS NULL OR "EducationMediumId" IS NULL) OR
			       EXISTS (SELECT 1 FROM "Finance_ChargeWaiverApplications" WHERE "EmployeeId" IS NOT NULL OR "StudentId" IS NULL OR length("Reason")>500) OR
			       EXISTS (SELECT 1 FROM "Finance_PaymentMethodRecords" WHERE "EmployeeId" IS NOT NULL) OR
			       EXISTS (SELECT 1 FROM "Finance_FeeRefundRequests" WHERE "EmployeeId" IS NOT NULL OR "FinanceExternalSourceLinkId" IS NOT NULL) OR
			       EXISTS (SELECT 1 FROM "Finance_CashCollections" WHERE "EmployeeId" IS NOT NULL) OR
			       EXISTS (SELECT 1 FROM "Finance_Transactions" WHERE "EmployeeId" IS NOT NULL) OR
			       EXISTS (SELECT 1 FROM "Finance_ExternalReceivableCommands" WHERE "ResultEntityId" IS NOT NULL OR "ResultEntityType" IS NOT NULL) OR
			       EXISTS (SELECT 1 FROM "AppBookIssues" WHERE "BorrowerScopeCaptureId" IS NOT NULL OR "BorrowerScopeCaptureVersion" IS NOT NULL OR "BorrowerScopeProvenance"<>0) OR
			       EXISTS (SELECT 1 FROM "AppBookReservations" WHERE "BorrowerScopeCaptureId" IS NOT NULL OR "BorrowerScopeCaptureVersion" IS NOT NULL) OR
			       EXISTS (SELECT 1 FROM "AppLibraryLoanEvents" WHERE "BeforeBorrowerScopeCaptureId" IS NOT NULL OR "AfterBorrowerScopeCaptureId" IS NOT NULL) OR
			       EXISTS (SELECT 1 FROM "AppLibraryReservationEvents" WHERE "BeforeBorrowerScopeCaptureId" IS NOT NULL OR "AfterBorrowerScopeCaptureId" IS NOT NULL) THEN
			        RAISE EXCEPTION 'Library typed settlement rollback cannot preserve recorded staff assignments, payer identity, currency, decisions or command results. Retain the schema and reconcile first.';
			    END IF;
			END;
			$library_typed_rollback$;
			""");
		PreflightSettlementMoneyRollback(migrationBuilder);
	}
}
