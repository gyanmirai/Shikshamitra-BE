using Microsoft.EntityFrameworkCore.Migrations;

namespace ShikshaMitra.Migrations;

public partial class CompleteLibraryCirculationAndFinanceSources
{
	private static void PreflightLegacySchoolDates(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		LOCK TABLE "AppBookIssues", "AppBookReservations", "AppLibraryCharges", "AppLibraryCopyEvents",
		    "AppLibraryBorrowingPolicyVersions", "Finance_Charges", "Finance_ChargeRequests" IN SHARE ROW EXCLUSIVE MODE;
		DO $library_upgrade$
		BEGIN
		    IF EXISTS (
		        SELECT 1 FROM "AppBookIssues" row
		        CROSS JOIN LATERAL (VALUES (row."IssueDate"), (row."DueDate"), (row."ReturnDate")) AS original(value)
		        WHERE original.value IS NOT NULL AND (NOT isfinite(original.value) OR
		            original.value < TIMESTAMPTZ '0001-01-01 00:00:00+00' OR original.value >= TIMESTAMPTZ '10000-01-01 00:00:00+00')
		    ) OR EXISTS (
		        SELECT 1 FROM "AppBookReservations" row
		        CROSS JOIN LATERAL (VALUES (row."RequestedDate"), (row."ReserveDueDate")) AS original(value)
		        WHERE original.value IS NOT NULL AND (NOT isfinite(original.value) OR
		            original.value < TIMESTAMPTZ '0001-01-01 00:00:00+00' OR original.value >= TIMESTAMPTZ '10000-01-01 00:00:00+00')
		    ) OR EXISTS (
		        SELECT 1 FROM "AppLibraryCharges" row
		        CROSS JOIN LATERAL (VALUES (row."ChargeDate"), (row."DueDate")) AS original(value)
		        WHERE original.value IS NOT NULL AND (NOT isfinite(original.value) OR
		            original.value < TIMESTAMPTZ '0001-01-01 00:00:00+00' OR original.value >= TIMESTAMPTZ '10000-01-01 00:00:00+00')
		    ) THEN
		        RAISE EXCEPTION 'Library upgrade requires school-date review: retain unsupported raw dates and establish supported civil dates before migrating.';
		    END IF;
		END;
		$library_upgrade$;
		""");

	private static void BackfillUnconfirmedLibraryDates(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
		UPDATE "AppBookIssues" SET
		    "SchoolIssueDate" = ("IssueDate" AT TIME ZONE 'UTC')::date,
		    "SchoolDueDate" = ("DueDate" AT TIME ZONE 'UTC')::date,
		    "SchoolReturnDate" = ("ReturnDate" AT TIME ZONE 'UTC')::date,
		    "IssueDateProvenance" = 0, "DueDateProvenance" = 0, "ReturnDateProvenance" = 0;
		UPDATE "AppBookReservations" SET
		    "OriginalMemberId" = "MemberId", "State" = 5,
		    "SchoolRequestedDate" = ("RequestedDate" AT TIME ZONE 'UTC')::date,
		    "SchoolRequestExpiryDate" = ("ReserveDueDate" AT TIME ZONE 'UTC')::date,
		    "RequestedDateProvenance" = 0, "RequestExpiryDateProvenance" = 0, "QueueProvenance" = 0,
		    "Revision" = md5("Id"::text || ':library-circulation-upgrade-v1'),
		    "ReviewReason" = 'Review the legacy member, school dates, academic context, and queue order before allocation.';
		UPDATE "AppLibraryCharges" SET
		    "SchoolChargeDate" = ("ChargeDate" AT TIME ZONE 'UTC')::date,
		    "SchoolDueDate" = ("DueDate" AT TIME ZONE 'UTC')::date,
		    "ChargeDateProvenance" = 0, "DueDateProvenance" = 0;
		""");
}
