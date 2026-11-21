using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCopyEvidenceMigrationPostgres_Tests
{
	[LibraryPostgresTheory]
	[InlineData("UPDATE \"AppLibraryCopyEvents\" SET \"Reason\" = 'Rewritten history'")]
	[InlineData("DELETE FROM \"AppLibraryCopyEvents\"")]
	[InlineData("TRUNCATE \"AppLibraryCopyEvents\"")]
	public async Task Existing_Copy_History_Should_Be_Append_Only_At_The_Database_Boundary(string command)
	{
		await using var fixture = await CreateUpgradedAsync();
		await fixture.ExecuteAsync(InsertEvent);

		var error = await Should.ThrowAsync<PostgresException>(() => fixture.ExecuteAsync(command));

		error.MessageText.ShouldContain("Library copy history is append-only");
		(await fixture.ScalarAsync("SELECT \"Reason\" FROM \"AppLibraryCopyEvents\""))
			.ShouldBe("Legacy damage inspected at desk");
	}

	[LibraryPostgresFact]
	public async Task Rollback_Should_Refuse_To_Drop_Existing_Copy_Evidence()
	{
		await using var fixture = await CreateUpgradedAsync();
		await fixture.ExecuteAsync(InsertEvent);

		var error = await Should.ThrowAsync<PostgresException>(() => fixture.ExecuteAsync(
			fixture.Script(LibraryMigrationPostgresFixture.CopyEvidence, LibraryMigrationPostgresFixture.PolicyInbox)));

		error.MessageText.ShouldContain("cannot preserve existing copy history");
		(await fixture.ScalarAsync("SELECT count(*) FROM \"AppLibraryCopyEvents\"")).ShouldBe("1");
	}

	[LibraryPostgresFact]
	public async Task Empty_Rollback_Should_Preserve_Preexisting_Copies_And_Notification_Evidence()
	{
		await using var fixture = await CreateUpgradedAsync();
		await fixture.ExecuteAsync(fixture.Script(LibraryMigrationPostgresFixture.CopyEvidence, LibraryMigrationPostgresFixture.PolicyInbox));

		(await fixture.ScalarAsync("SELECT count(*) FROM \"AppBookCopies\"")).ShouldBe("3");
		(await fixture.ScalarAsync("SELECT count(*) FROM \"AppLibraryNotificationHistories\" WHERE \"Status\" = 'Sent'"))
			.ShouldBe("1");
		(await fixture.ScalarAsync("SELECT count(*) FROM information_schema.tables WHERE table_name = 'AppLibraryCopyEvents'"))
			.ShouldBe("0");
	}

	[LibraryPostgresFact]
	public async Task One_Copy_Should_Not_Have_Two_Events_With_The_Same_Sequence()
	{
		await using var fixture = await CreateUpgradedAsync();
		await fixture.ExecuteAsync(InsertEvent);

		var error = await Should.ThrowAsync<PostgresException>(() => fixture.ExecuteAsync(InsertEvent));

		error.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
		error.ConstraintName.ShouldBe("IX_AppLibraryCopyEvents_TenantId_CopyId_Sequence");
	}

	private static async Task<LibraryMigrationPostgresFixture> CreateUpgradedAsync()
	{
		var fixture = await LibraryPolicyInboxMigrationPostgres_Tests.CreateUpgradedAsync();
		try
		{
			await fixture.ExecuteAsync("""
				CREATE TABLE "AppBookIssues" ("Id" uuid PRIMARY KEY);
				CREATE TABLE "AppLibraryShelves" ("Id" uuid PRIMARY KEY);
				INSERT INTO "AppLibrarySections" VALUES ('61000000-0000-0000-0000-000000000001');
				INSERT INTO "AppLibraryShelves" VALUES ('62000000-0000-0000-0000-000000000001');
				""");
			await fixture.ExecuteAsync(fixture.Script(LibraryMigrationPostgresFixture.PolicyInbox, LibraryMigrationPostgresFixture.CopyEvidence));
			return fixture;
		}
		catch
		{
			await fixture.DisposeAsync();
			throw;
		}
	}

	private const string InsertEvent = """
		INSERT INTO "AppLibraryCopyEvents" (
		"Id", "TenantId", "BookId", "CopyId", "AccessionNumber", "Sequence", "EventType", "OccurredAt", "ActorId", "Reason",
		"PreviousStatus", "NextStatus", "PreviousRemarks", "NextRemarks", "FromSectionId", "FromShelfId", "ToSectionId", "ToShelfId", "RelatedLoanId")
		VALUES (gen_random_uuid(), 'a0000000-0000-0000-0000-000000000001',
		'10000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', 'Acc-001', 1, 0,
		NOW(), '71000000-0000-0000-0000-000000000001', 'Legacy damage inspected at desk', 0, 4, NULL, 'Existing damage evidence',
		'61000000-0000-0000-0000-000000000001', '62000000-0000-0000-0000-000000000001',
		'61000000-0000-0000-0000-000000000001', '62000000-0000-0000-0000-000000000001', NULL);
		""";
}
