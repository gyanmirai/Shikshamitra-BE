using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryTypedSettlementMigrationPostgres_Tests
{
	[LibraryPostgresFact]
	public async Task A_Complete_Assignment_Set_Should_Commit_Once_And_Reject_A_Later_Child()
	{
		await using var database = await CreateM4Async();
		await UpgradeAsync(database);
		await database.ExecuteAsync("BEGIN;\n" + CaptureHeader + FirstTwoChildren + "\nCOMMIT;");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryBorrowerAssignmentSnapshots\";")).ShouldBe("2");
		var error = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(ThirdChild));
		error.MessageText.ShouldContain("sealed assignment set");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryBorrowerAssignmentSnapshots\";")).ShouldBe("2");
	}

	[LibraryPostgresFact]
	public async Task An_Incomplete_Assignment_Set_Should_Reject_The_Whole_Transaction_At_Commit()
	{
		await using var database = await CreateM4Async();
		await UpgradeAsync(database);
		var error = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync("BEGIN;\n" + CaptureHeader + "\nCOMMIT;"));
		error.MessageText.ShouldContain("sealed assignment set");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryBorrowerScopeCaptures\";")).ShouldBe("0");
	}

	[LibraryPostgresTheory]
	[InlineData("AppLibraryBorrowerScopeCaptures", "SourceName")]
	[InlineData("AppLibraryBorrowerAssignmentSnapshots", "StageId")]
	public async Task Recorded_Assignment_Evidence_Should_Reject_Update_Delete_And_Truncate(string table, string column)
	{
		await using var database = await CreateM4Async();
		await UpgradeAsync(database);
		await database.ExecuteAsync("BEGIN;\n" + CaptureHeader + FirstTwoChildren + "\nCOMMIT;");
		var newValue = column == "StageId" ? "'d1000000-0000-0000-0000-000000000099'::uuid" : "'Rewritten source'";
		string[] commands = [$"UPDATE \"{table}\" SET \"{column}\"={newValue}", $"DELETE FROM \"{table}\"", $"TRUNCATE \"{table}\" CASCADE"];
		foreach (var command in commands)
		{
			var error = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(command));
			error.MessageText.ShouldContain("append-only");
			(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryBorrowerScopeCaptures\";")).ShouldBe("1");
			(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryBorrowerAssignmentSnapshots\";")).ShouldBe("2");
		}
	}

	[LibraryPostgresFact]
	public async Task Rollback_Should_Refuse_To_Destroy_Recorded_Staff_Assignment_Evidence()
	{
		await using var database = await CreateM4Async();
		await UpgradeAsync(database);
		await database.ExecuteAsync("BEGIN;\n" + CaptureHeader + FirstTwoChildren + "\nCOMMIT;");
		var error = await Should.ThrowAsync<PostgresException>(() => RollbackAsync(database));
		error.MessageText.ShouldContain("cannot preserve");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryBorrowerAssignmentSnapshots\";")).ShouldBe("2");
	}

	[LibraryPostgresFact]
	public async Task System_Capture_Should_Require_A_Known_Nonnull_Purpose_At_The_Database_Boundary()
	{
		await using var database = await CreateM4Async();
		await UpgradeAsync(database);
		var invalidActor = CaptureHeader.Replace(
			"1,'71000000-0000-0000-0000-000000000001',NULL", "2,NULL,NULL");
		invalidActor.ShouldNotBe(CaptureHeader);
		var error = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(invalidActor));
		error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryBorrowerScopeCaptures\";")).ShouldBe("0");
	}

	private const string CaptureHeader = """
		INSERT INTO "AppLibraryBorrowerScopeCaptures" ("Id","TenantId","SourceKind","SourceId","CanonicalEmployeeId","AcademicYearId","Version","Provenance","IsComplete","ExpectedAssignmentCount","SourceName","CapturedAt","ActorKind","ActorId","SystemPurpose")
		VALUES ('b1000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001',1,'83000000-0000-0000-0000-000000000001','b2000000-0000-0000-0000-000000000001','81000000-0000-0000-0000-000000000001',1,1,true,2,'EmployeeOrganizationAssignments','2026-11-17T03:00:00Z',1,'71000000-0000-0000-0000-000000000001',NULL);
		""";
	private const string FirstTwoChildren = """
		INSERT INTO "AppLibraryBorrowerAssignmentSnapshots" ("Id","TenantId","CaptureId","OrganizationAssignmentId","StageId","MediumId","BoardId","AcademicYearId") VALUES
		('b3000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','b1000000-0000-0000-0000-000000000001','c1000000-0000-0000-0000-000000000001','d1000000-0000-0000-0000-000000000001','f1000000-0000-0000-0000-000000000001','e1000000-0000-0000-0000-000000000001','81000000-0000-0000-0000-000000000001'),
		('b3000000-0000-0000-0000-000000000002','a0000000-0000-0000-0000-000000000001','b1000000-0000-0000-0000-000000000001','c1000000-0000-0000-0000-000000000002','d1000000-0000-0000-0000-000000000002','f1000000-0000-0000-0000-000000000001','e1000000-0000-0000-0000-000000000001','81000000-0000-0000-0000-000000000001');
		""";
	private const string ThirdChild = """
		INSERT INTO "AppLibraryBorrowerAssignmentSnapshots" ("Id","TenantId","CaptureId","OrganizationAssignmentId","StageId","MediumId","BoardId","AcademicYearId") VALUES
		('b3000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000001','b1000000-0000-0000-0000-000000000001','c1000000-0000-0000-0000-000000000003','d1000000-0000-0000-0000-000000000003','f1000000-0000-0000-0000-000000000001','e1000000-0000-0000-0000-000000000001','81000000-0000-0000-0000-000000000001');
		""";
}
