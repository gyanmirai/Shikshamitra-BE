using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryPolicyInboxMigrationPostgres_Tests
{
	[LibraryPostgresFact]
	public async Task Upgrade_Should_Keep_Legacy_Delivery_Audit_Outside_Private_Inboxes_And_Preserve_Daily_Recurrence()
	{
		await using var fixture = await CreateUpgradedAsync();

		(await fixture.ScalarAsync("SELECT count(*) FROM \"AppLibraryNotificationHistories\" WHERE \"RecipientUserId\" IS NOT NULL OR \"ReadAt\" IS NOT NULL"))
			.ShouldBe("0");
		(await fixture.ScalarAsync("SELECT \"Status\" FROM \"AppLibraryNotificationHistories\" WHERE \"DeduplicationKey\" = 'original-history'"))
			.ShouldBe("Sent");
		(await fixture.ScalarAsync("SELECT \"RepeatIntervalDays\" FROM \"AppLibraryNotificationSettings\""))
			.ShouldBe("1");
	}

	[LibraryPostgresFact]
	public async Task Empty_Feature_Rollback_Should_Retain_All_Preexisting_Library_Records()
	{
		await using var fixture = await CreateUpgradedAsync();
		await fixture.ExecuteAsync(fixture.Script(LibraryMigrationPostgresFixture.PolicyInbox, LibraryMigrationPostgresFixture.Foundation));

		(await fixture.ScalarAsync("SELECT count(*) FROM \"AppBookCopies\"")).ShouldBe("3");
		(await fixture.ScalarAsync("SELECT count(*) FROM \"AppLibraryNotificationHistories\" WHERE \"Status\" = 'Sent'"))
			.ShouldBe("1");
		(await fixture.ScalarAsync("SELECT count(*) FROM information_schema.columns WHERE table_name = 'AppLibraryNotificationHistories' AND column_name = 'RecipientUserId'"))
			.ShouldBe("0");
	}

	[LibraryPostgresTheory]
	[InlineData("policy")]
	[InlineData("closure")]
	[InlineData("recipient")]
	[InlineData("recurrence")]
	public async Task Rollback_Should_Refuse_To_Delete_Existing_Feature_Evidence(string evidence)
	{
		await using var fixture = await CreateUpgradedAsync();
		await fixture.ExecuteAsync(evidence switch
		{
			"policy" => """
				INSERT INTO "AppLibraryBorrowingPolicyVersions"
				("Id", "TenantId", "OriginAcademicYearId", "PolicyId", "Version", "Name", "Role", "EffectiveFrom", "Priority",
				"MaxBooksAtTime", "ReservationLimit", "LoanDays", "DayCountingMode", "MaxRenewals", "RenewalDays", "PickupWindowDays",
				"MoveDueDateToNextOpenDay", "Status", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted") VALUES
				(gen_random_uuid(), NULL, '51000000-0000-0000-0000-000000000001', gen_random_uuid(), 1, 'Reading', 0,
				'2026-11-16', 0, 3, 2, 14, 0, 1, 7, 2, true, 1, '{}', 'migration-fixture', NOW(), false);
				""",
			"closure" => """
				INSERT INTO "AppLibraryCalendarClosures"
				("Id", "TenantId", "FromDate", "ToDate", "Reason", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted") VALUES
				(gen_random_uuid(), NULL, '2026-11-16', '2026-11-16', 'School closure', '{}', 'migration-fixture', NOW(), false);
				""",
			"recipient" => "UPDATE \"AppLibraryNotificationHistories\" SET \"RecipientUserId\" = gen_random_uuid(), \"ReadAt\" = NOW();",
			_ => "UPDATE \"AppLibraryNotificationSettings\" SET \"RepeatIntervalDays\" = 2;"
		});

		var error = await Should.ThrowAsync<PostgresException>(() => fixture.ExecuteAsync(
			fixture.Script(LibraryMigrationPostgresFixture.PolicyInbox, LibraryMigrationPostgresFixture.Foundation)));

		error.MessageText.ShouldContain("cannot preserve existing policy, calendar, recipient or recurrence evidence");
		(await fixture.ScalarAsync("SELECT count(*) FROM information_schema.columns WHERE table_name = 'AppLibraryNotificationHistories' AND column_name = 'RecipientUserId'"))
			.ShouldBe("1");
		(await fixture.ScalarAsync("SELECT count(*) FROM \"AppBookCopies\"")).ShouldBe("3");
	}

	internal static async Task<LibraryMigrationPostgresFixture> CreateUpgradedAsync()
	{
		var fixture = await LibraryMigrationPostgresFixture.CreateAsync();
		try
		{
			await fixture.ExecuteAsync(fixture.Script());
			await fixture.ExecuteAsync("""
			CREATE TABLE "AppBookCategories" ("Id" uuid PRIMARY KEY);
			CREATE TABLE "AppLibrarySections" ("Id" uuid PRIMARY KEY);
			CREATE TABLE "academic_Years" ("Id" uuid PRIMARY KEY);
			CREATE TABLE "academic_EducationMediumStages" ("Id" uuid PRIMARY KEY);
			CREATE TABLE "academic_Standards" ("Id" uuid PRIMARY KEY);
			INSERT INTO "academic_Years" VALUES ('51000000-0000-0000-0000-000000000001');
			""");
			await fixture.ExecuteAsync(fixture.Script(LibraryMigrationPostgresFixture.Foundation, LibraryMigrationPostgresFixture.PolicyInbox));
			return fixture;
		}
		catch
		{
			await fixture.DisposeAsync();
			throw;
		}
	}
}
