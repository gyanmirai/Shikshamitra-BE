using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCirculationMigrationPostgres_Tests
{
	[LibraryPostgresTheory]
	[InlineData("AppLibraryLoanCommands", "UPDATE \"AppLibraryLoanCommands\" SET \"ResultJson\"='changed'")]
	[InlineData("AppLibraryLoanCommands", "DELETE FROM \"AppLibraryLoanCommands\"")]
	[InlineData("AppLibraryLoanCommands", "TRUNCATE \"AppLibraryLoanCommands\"")]
	[InlineData("AppLibraryLoanEvents", "UPDATE \"AppLibraryLoanEvents\" SET \"Reason\"='changed'")]
	[InlineData("AppLibraryLoanEvents", "DELETE FROM \"AppLibraryLoanEvents\"")]
	[InlineData("AppLibraryLoanEvents", "TRUNCATE \"AppLibraryLoanEvents\"")]
	public async Task Accepted_Loan_Commands_And_Events_Should_Be_Append_Only_In_Postgres(string table, string command)
	{
		await using var database = await template.CloneAsync();
		await UpgradeAsync(database);
		await database.ExecuteAsync(LoanEvidence);
		var error = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(command));
		error.MessageText.ShouldContain("append-only");
		(await database.ScalarAsync($"SELECT count(*) FROM \"{table}\";")).ShouldBe("1");
	}

	[LibraryPostgresFact]
	public async Task Rollback_Should_Refuse_To_Destroy_An_Accepted_Command_Or_Loan_Event()
	{
		await using var database = await template.CloneAsync();
		await UpgradeAsync(database);
		await database.ExecuteAsync(LoanEvidence);
		var error = await Should.ThrowAsync<PostgresException>(() => RollbackAsync(database));
		error.MessageText.ShouldContain("cannot preserve");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryLoanCommands\";")).ShouldBe("1");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryLoanEvents\";")).ShouldBe("1");
	}

	[LibraryPostgresTheory]
	[InlineData("UPDATE \"AppBookIssues\" SET \"SchoolIssueDate\"='2026-11-17', \"IssueDateProvenance\"=2;")]
	[InlineData("UPDATE \"AppLibraryBorrowingPolicyVersions\" SET \"DueDateMode\"=0;")]
	public async Task Rollback_Should_Preserve_Reviewed_Civil_Dates_And_Explicit_Manual_Policies(string newEvidence)
	{
		await using var database = await template.CloneAsync();
		await UpgradeAsync(database);
		await database.ExecuteAsync(newEvidence);
		var error = await Should.ThrowAsync<PostgresException>(() => RollbackAsync(database));
		error.MessageText.ShouldContain("cannot preserve");
		(await database.ScalarAsync("SELECT count(*) FROM information_schema.columns WHERE table_name='AppBookIssues' AND column_name='SchoolIssueDate';")).ShouldBe("1");
	}

	[LibraryPostgresFact]
	public async Task Rollback_Of_Only_Unconfirmed_Backfill_Should_Retain_Raw_History_And_Original_Column_Defaults()
	{
		await using var database = await template.CloneAsync();
		await UpgradeAsync(database);
		await RollbackAsync(database);
		(await database.ScalarAsync("""
			SELECT "IssueDate"='2026-11-17T00:15:00+05:30'::timestamptz AND "DueDate"='2026-11-24T00:15:00+05:30'::timestamptz
			FROM "AppBookIssues"
			""")).ShouldBe("True");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryCopyEvents\";")).ShouldBe("1");
		(await database.ScalarAsync("SELECT column_default FROM information_schema.columns WHERE table_name='Finance_Charges' AND column_name='StudentId';")).ShouldBeNull();
		(await database.ScalarAsync("SELECT column_default FROM information_schema.columns WHERE table_name='AppBookIssues' AND column_name='IssueDate';")).ShouldBeNull();
	}

	private const string LoanEvidence = """
		INSERT INTO "AppLibraryLoanCommands" ("Id","TenantId","ActorId","Operation","IdempotencyKey","PayloadFingerprint","LoanId","ResultJson","RecordedAt")
		VALUES ('91000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','71000000-0000-0000-0000-000000000001',1,'accepted-original',repeat('a',64),'83000000-0000-0000-0000-000000000001','{}',NOW());
		INSERT INTO "AppLibraryLoanEvents" ("Id","TenantId","LoanId","Sequence","EventKind","ActorId","Reason","BeforeJson","AfterJson","RecordedAt")
		VALUES ('92000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','83000000-0000-0000-0000-000000000001',1,4,'71000000-0000-0000-0000-000000000001','Original event','{}','{}',NOW());
		""";
}
