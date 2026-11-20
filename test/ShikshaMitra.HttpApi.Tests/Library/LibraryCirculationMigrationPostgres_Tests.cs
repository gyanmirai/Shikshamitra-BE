using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

[Collection(LibraryCirculationMigrationCollection.Name)]
public partial class LibraryCirculationMigrationPostgres_Tests(LibraryCirculationMigrationTemplate template)
{
	[LibraryPostgresTheory]
	[InlineData("Pacific/Kiritimati")]
	[InlineData("Pacific/Honolulu")]
	public async Task Upgrade_Should_Keep_Raw_Instants_And_Mark_Utc_Components_Unconfirmed_Independently_Of_Session_Zone(string zone)
	{
		await using var database = await template.CloneAsync();
		await UpgradeAsync(database, zone);
		(await database.ScalarAsync("""
			SELECT "SchoolIssueDate"::text||'|'||"SchoolDueDate"::text||'|'||"SchoolReturnDate"::text||'|'||
			"IssueDateProvenance"||'|'||"DueDateProvenance"||'|'||"ReturnDateProvenance"
			FROM "AppBookIssues"
			""")).ShouldBe("2026-11-16|2026-11-23|2026-11-24|0|0|0");
		(await database.ScalarAsync("""
			SELECT "IssueDate"='2026-11-17T00:15:00+05:30'::timestamptz AND
			"DueDate"='2026-11-24T00:15:00+05:30'::timestamptz AND
			"ReturnDate"='2026-11-25T00:15:00+05:30'::timestamptz AND
			"OriginalSchoolDueDate" IS NULL AND "AcademicYearId" IS NULL AND "IssuedByUserId" IS NULL AND
			"PolicySnapshotJson" IS NULL AND "BorrowerSnapshotJson" IS NULL FROM "AppBookIssues"
			""")).ShouldBe("True");
	}

	[LibraryPostgresFact]
	public async Task Upgrade_Should_Retain_Legacy_Reservation_Identity_Without_Inventing_Member_Year_Or_Fifo_Order()
	{
		await using var database = await template.CloneAsync();
		await UpgradeAsync(database);
		(await database.ScalarAsync("""
			SELECT "State"=5 AND "OriginalMemberId"="MemberId" AND "CanonicalMemberId" IS NULL AND
			"MemberRole" IS NULL AND "AcademicYearId" IS NULL AND "QueueSequence" IS NULL AND "QueueProvenance"=0 AND
			"SchoolRequestedDate"='2026-11-16'::date AND "SchoolRequestExpiryDate"='2026-11-23'::date AND
			"RequestedDateProvenance"=0 AND "RequestExpiryDateProvenance"=0 AND length("Revision")>0 AND
			length("ReviewReason")>0 AND "BorrowerScopeProvenance"=0 AND "BorrowerScopeSnapshotJson" IS NULL AND
			"RequestedDate"='2026-11-17T00:15:00+05:30'::timestamptz FROM "AppBookReservations"
			""")).ShouldBe("True");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryHolds\";")).ShouldBe("0");
	}

	[LibraryPostgresFact]
	public async Task Upgrade_Should_Preserve_Declared_Payment_And_Existing_Copy_Actor_Without_Fabricating_Finance_Evidence()
	{
		await using var database = await template.CloneAsync();
		await UpgradeAsync(database);
		(await database.ScalarAsync("""
			SELECT "Amount"=37.50 AND "PaymentStatus"=1 AND "PaidDate"='2026-11-23T12:45:00+05:30'::timestamptz AND
			"ChargeDate"='2026-11-17T00:15:00+05:30'::timestamptz AND "DueDate"='2026-11-24T00:15:00+05:30'::timestamptz AND
			"SchoolChargeDate"='2026-11-16'::date AND "SchoolDueDate"='2026-11-23'::date AND
			"ChargeDateProvenance"=0 AND "DueDateProvenance"=0 AND "AcademicYearId" IS NULL AND
			"AcademicYearProvenance"=0 AND "Currency" IS NULL AND "FinanceSourceLinkId" IS NULL FROM "AppLibraryCharges"
			""")).ShouldBe("True");
		(await database.ScalarAsync("SELECT count(*) FROM \"Finance_ExternalReceivableSourceLinks\";")).ShouldBe("0");
		(await database.ScalarAsync("""
			SELECT "ActorKind"=1 AND "ActorId"='71000000-0000-0000-0000-000000000001'::uuid AND
			"SystemPurpose" IS NULL AND "Reason"='Original actor evidence' FROM "AppLibraryCopyEvents"
			""")).ShouldBe("True");
		(await database.ScalarAsync("SELECT \"DueDateMode\" FROM \"AppLibraryBorrowingPolicyVersions\";")).ShouldBe("1");
	}

	[LibraryPostgresTheory]
	[InlineData("infinity")]
	[InlineData("12000-01-01")]
	public async Task Unsupported_Legacy_Date_Should_Stop_The_Upgrade_Atomically_With_Review_Required(string rawDate)
	{
		await using var database = await template.CloneAsync();
		await database.ExecuteAsync($"UPDATE \"AppBookIssues\" SET \"IssueDate\"='{rawDate}'::timestamptz;");
		var error = await Should.ThrowAsync<PostgresException>(() => UpgradeAsync(database));
		error.MessageText.ShouldContain("school-date review");
		(await database.ScalarAsync("SELECT count(*) FROM information_schema.columns WHERE table_name='AppBookIssues' AND column_name='SchoolIssueDate';")).ShouldBe("0");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryCopyEvents\";")).ShouldBe("1");
	}

	private static Task UpgradeAsync(LibraryMigrationPostgresFixture database, string zone = "UTC") => database.ExecuteAsync(
		$"SET TIME ZONE '{zone}';\n" + database.Script(LibraryMigrationPostgresFixture.CopyEvidence, LibraryMigrationPostgresFixture.CirculationEvidence));
	private static Task RollbackAsync(LibraryMigrationPostgresFixture database) => database.ExecuteAsync(
		database.Script(LibraryMigrationPostgresFixture.CirculationEvidence, LibraryMigrationPostgresFixture.CopyEvidence));
}
