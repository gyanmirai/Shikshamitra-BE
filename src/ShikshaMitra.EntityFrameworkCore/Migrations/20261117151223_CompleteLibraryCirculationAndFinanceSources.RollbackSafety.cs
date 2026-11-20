using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class CompleteLibraryCirculationAndFinanceSources
{
	private static void RefuseLossyCirculationRollback(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		LOCK TABLE "AppLibraryLoanCommands", "AppLibraryLoanEvents", "AppLibraryReservationEvents", "AppLibraryHolds",
		    "Finance_ExternalReceivableCommands", "Finance_ExternalReceivableSourceLinks", "AppBookIssues",
		    "AppBookReservations", "AppLibraryCharges", "AppLibraryCopyEvents", "AppLibraryBorrowingPolicyVersions",
		    "Finance_Charges", "Finance_ChargeRequests" IN ACCESS EXCLUSIVE MODE;
		DO $library_rollback$
		BEGIN
		    IF EXISTS (SELECT 1 FROM "AppLibraryLoanCommands") OR EXISTS (SELECT 1 FROM "AppLibraryLoanEvents") OR
		       EXISTS (SELECT 1 FROM "AppLibraryReservationEvents") OR EXISTS (SELECT 1 FROM "AppLibraryHolds") OR
		       EXISTS (SELECT 1 FROM "Finance_ExternalReceivableCommands") OR EXISTS (SELECT 1 FROM "Finance_ExternalReceivableSourceLinks") OR
		       EXISTS (SELECT 1 FROM "AppLibraryCopyEvents" WHERE "ActorKind"<>1 OR "ActorId" IS NULL OR "SystemPurpose" IS NOT NULL) OR
		       EXISTS (SELECT 1 FROM "AppLibraryBorrowingPolicyVersions" WHERE "DueDateMode"<>1) OR
		       EXISTS (SELECT 1 FROM "Finance_Charges" WHERE "EmployeeId" IS NOT NULL OR "StudentId" IS NULL OR "StandardId" IS NULL OR
		           "DivisionId" IS NULL OR "BoardId" IS NULL OR "EducationMediumId" IS NULL OR "DueDate" IS NULL OR
		           "SchoolAssessmentDate" IS NOT NULL OR "SchoolDueDate" IS NOT NULL OR "DueDateProvenance"<>0) OR
		       EXISTS (SELECT 1 FROM "Finance_ChargeRequests" WHERE "BoardId" IS NULL OR "EducationMediumId" IS NULL OR
		           "DueDate" IS NULL OR "SchoolDueDate" IS NOT NULL OR "DueDateProvenance"<>0) OR
		       EXISTS (SELECT 1 FROM "AppBookIssues" WHERE "IssueDate" IS NULL OR "DueDate" IS NULL OR
		           "IssueDateProvenance"<>0 OR "DueDateProvenance"<>0 OR "ReturnDateProvenance"<>0 OR
		           "OriginalSchoolDueDate" IS NOT NULL OR "AcademicYearId" IS NOT NULL OR "IssuedByUserId" IS NOT NULL OR
		           "IssueRecordedAt" IS NOT NULL OR "PolicySnapshotJson" IS NOT NULL OR "BorrowerSnapshotJson" IS NOT NULL OR
		           "OriginalCalendarSnapshotJson" IS NOT NULL OR "RenewalCount"<>0 OR "BorrowerBoardId" IS NOT NULL OR
		           "BorrowerMediumId" IS NOT NULL OR "BorrowerStageId" IS NOT NULL OR "BorrowerStandardId" IS NOT NULL OR "BorrowerDivisionId" IS NOT NULL OR
		           ("SchoolIssueDate" IS NOT NULL AND "SchoolIssueDate" IS DISTINCT FROM ("IssueDate" AT TIME ZONE 'UTC')::date) OR
		           ("SchoolDueDate" IS NOT NULL AND "SchoolDueDate" IS DISTINCT FROM ("DueDate" AT TIME ZONE 'UTC')::date) OR
		           ("SchoolReturnDate" IS NOT NULL AND "SchoolReturnDate" IS DISTINCT FROM ("ReturnDate" AT TIME ZONE 'UTC')::date)) OR
		       EXISTS (SELECT 1 FROM "AppBookReservations" WHERE "RequestedDate" IS NULL OR "ReserveDueDate" IS NULL OR
		           "State"<>5 OR "CanonicalMemberId" IS NOT NULL OR "MemberRole" IS NOT NULL OR "AcademicYearId" IS NOT NULL OR
		           "QueueSequence" IS NOT NULL OR "QueueProvenance"<>0 OR "RequestedDateProvenance"<>0 OR "RequestExpiryDateProvenance"<>0 OR
		           "OriginalMemberId" IS DISTINCT FROM "MemberId" OR "PolicySnapshotJson" IS NOT NULL OR "TerminalReason" IS NOT NULL OR "EndedAt" IS NOT NULL OR
		           "BorrowerScopeProvenance"<>0 OR "BorrowerScopeSnapshotJson" IS NOT NULL OR "BorrowerBoardId" IS NOT NULL OR
		           "BorrowerMediumId" IS NOT NULL OR "BorrowerStageId" IS NOT NULL OR "BorrowerStandardId" IS NOT NULL OR "BorrowerDivisionId" IS NOT NULL OR
		           "ReviewReason" IS DISTINCT FROM 'Review the legacy member, school dates, academic context, and queue order before allocation.' OR
		           ("SchoolRequestedDate" IS NOT NULL AND "SchoolRequestedDate" IS DISTINCT FROM ("RequestedDate" AT TIME ZONE 'UTC')::date) OR
		           ("SchoolRequestExpiryDate" IS NOT NULL AND "SchoolRequestExpiryDate" IS DISTINCT FROM ("ReserveDueDate" AT TIME ZONE 'UTC')::date)) OR
		       EXISTS (SELECT 1 FROM "AppLibraryCharges" WHERE "ChargeDate" IS NULL OR "DueDate" IS NULL OR
		           "ChargeDateProvenance"<>0 OR "DueDateProvenance"<>0 OR "AcademicYearId" IS NOT NULL OR "AcademicYearProvenance"<>0 OR
		           "AcademicYearEvidenceJson" IS NOT NULL OR "SourceDateEvidenceJson" IS NOT NULL OR "Currency" IS NOT NULL OR "FinanceSourceLinkId" IS NOT NULL OR
		           ("SchoolChargeDate" IS NOT NULL AND "SchoolChargeDate" IS DISTINCT FROM ("ChargeDate" AT TIME ZONE 'UTC')::date) OR
		           ("SchoolDueDate" IS NOT NULL AND "SchoolDueDate" IS DISTINCT FROM ("DueDate" AT TIME ZONE 'UTC')::date)) THEN
		        RAISE EXCEPTION 'Library circulation rollback cannot preserve recorded school dates, policy choices, holds or Finance evidence. Retain the schema and reconcile before rollback.';
		    END IF;
		END;
		$library_rollback$;
		""");
}
