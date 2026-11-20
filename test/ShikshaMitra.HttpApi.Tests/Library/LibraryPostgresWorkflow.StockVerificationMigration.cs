using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private const string StockVerificationSessionId = "bd100000-0000-0000-0000-000000000001";
	private const string StockVerificationActorId = "bd100000-0000-0000-0000-000000000002";
	private const string StockVerificationBaselineId = "bd100000-0000-0000-0000-000000000003";
	private const string StockVerificationObservationId = "bd100000-0000-0000-0000-000000000004";
	private static readonly string StockVerificationSession = $$"""
		INSERT INTO "AppLibraryStockVerificationSessions"
		  ("Id", "TenantId", "Name", "SchoolDate", "ExpectedCopyCount", "ObservationCount", "Status", "OpenedBy", "OpenedAt",
		   "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
		VALUES ('{{StockVerificationSessionId}}', '{{LibraryMigrationPostgresFixture.TenantA}}', 'Counting the collection', '2026-11-19', 1, 0, 1,
		  '{{StockVerificationActorId}}', '2026-11-19T03:00:00+00', '{}', 'initial-evidence', '2026-11-19T03:00:00+00', FALSE);
		""";

	[LibraryPostgresFact]
	public async Task Stock_Verification_Postgres_Migration_Should_Preserve_Catalogue_And_Seal_Scoped_Evidence()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.SourceFingerprintEvidence);
		await database.ExecuteAsync(CatalogueLegacyRows);
		var oldCatalogue = await database.ScalarAsync(CataloguePreservedState);
		var migration = await StockVerificationMigrationAsync(database);
		(await database.ScalarAsync(CataloguePreservedState)).ShouldBe(oldCatalogue);
		await MigrateStockVerificationAsync(database, LibraryMigrationPostgresFixture.SourceFingerprintEvidence);
		(await database.ScalarAsync("SELECT to_regclass('public.\"AppLibraryStockVerificationSessions\"') IS NULL;")).ShouldBe("True");
		await MigrateStockVerificationAsync(database, migration);
		await database.ExecuteAsync(StockVerificationSession);
		var tenant = LibraryMigrationPostgresFixture.TenantA;
		await database.ExecuteAsync(StockBaseline(StockVerificationBaselineId, tenant));
		var foreign = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(StockBaseline(
			"bd100000-0000-0000-0000-000000000005", LibraryMigrationPostgresFixture.TenantB)));
		foreign.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
		var repeat = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(StockBaseline(
			"bd100000-0000-0000-0000-000000000005", tenant)));
		repeat.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
		await database.ExecuteAsync($$"""
			UPDATE "AppLibraryStockVerificationSessions" SET "ObservationCount"=1, "LastObservedAt"='2026-11-19T03:30:00+00'
			WHERE "Id"='{{StockVerificationSessionId}}';
			INSERT INTO "AppLibraryStockVerificationObservations"
			 ("Id", "TenantId", "SessionId", "Sequence", "AccessionNumber", "SchoolDate", "ObservedBy", "ObservedAt")
			VALUES ('{{StockVerificationObservationId}}', '{{tenant}}', '{{StockVerificationSessionId}}', 1, '00042', '2026-11-19',
			 '{{StockVerificationActorId}}', '2026-11-19T03:30:00+00');
			""");
		var repeatSequence = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync($$"""
			INSERT INTO "AppLibraryStockVerificationObservations"
			 ("Id", "TenantId", "SessionId", "Sequence", "AccessionNumber", "SchoolDate", "ObservedBy", "ObservedAt")
			SELECT 'bd100000-0000-0000-0000-000000000005'::uuid, "TenantId", "SessionId", "Sequence", "AccessionNumber",
			 "SchoolDate", "ObservedBy", "ObservedAt" FROM "AppLibraryStockVerificationObservations" WHERE "Id"='{{StockVerificationObservationId}}';
			"""));
		repeatSequence.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
		var baselineChange = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(
			$"UPDATE \"AppLibraryStockVerificationBaselines\" SET \"AccessionNumber\"='changed' WHERE \"Id\"='{StockVerificationBaselineId}';"));
		baselineChange.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		var scanDelete = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(
			$"DELETE FROM \"AppLibraryStockVerificationObservations\" WHERE \"Id\"='{StockVerificationObservationId}';"));
		scanDelete.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		(await database.ScalarAsync($"SELECT \"AccessionNumber\" FROM \"AppLibraryStockVerificationObservations\" WHERE \"Id\"='{StockVerificationObservationId}';")).ShouldBe("00042");
		var baselineCountChange = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(
			$"UPDATE \"AppLibraryStockVerificationSessions\" SET \"ExpectedCopyCount\"=5 WHERE \"Id\"='{StockVerificationSessionId}';"));
		baselineCountChange.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		await database.ExecuteAsync($$"""
			UPDATE "AppLibraryStockVerificationSessions" SET "Status"=2, "ReviewStartedBy"='{{StockVerificationActorId}}',
			  "ReviewStartedAt"='2026-11-19T04:00:00+00' WHERE "Id"='{{StockVerificationSessionId}}';
			UPDATE "AppLibraryStockVerificationSessions" SET "Status"=3, "FinalizedBy"='{{StockVerificationActorId}}',
			  "FinalizedAt"='2026-11-19T04:30:00+00', "FinalizedEvidenceFingerprint"=repeat('A', 64) WHERE "Id"='{{StockVerificationSessionId}}';
			""");
		var lateObservation = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync($$"""
			INSERT INTO "AppLibraryStockVerificationObservations"
			 ("Id", "TenantId", "SessionId", "Sequence", "AccessionNumber", "SchoolDate", "ObservedBy", "ObservedAt")
			VALUES ('bd100000-0000-0000-0000-000000000005', '{{tenant}}', '{{StockVerificationSessionId}}', 2, '00043', '2026-11-19',
			 '{{StockVerificationActorId}}', '2026-11-19T04:35:00+00');
			"""));
		lateObservation.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		var closedEdit = await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(
			$"UPDATE \"AppLibraryStockVerificationSessions\" SET \"LastModificationTime\"=CURRENT_TIMESTAMP WHERE \"Id\"='{StockVerificationSessionId}';"));
		closedEdit.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		(await database.ScalarAsync(CataloguePreservedState)).ShouldBe(oldCatalogue);
	}

	[LibraryPostgresFact]
	public async Task Stock_Verification_Postgres_Migration_Should_Refuse_To_Remove_Retained_Counts()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.SourceFingerprintEvidence);
		var migration = await StockVerificationMigrationAsync(database);
		await database.ExecuteAsync(StockVerificationSession);
		var refused = await Should.ThrowAsync<PostgresException>(() => MigrateStockVerificationAsync(database, LibraryMigrationPostgresFixture.SourceFingerprintEvidence));
		refused.SqlState.ShouldBe("P0001");
		refused.MessageText.ShouldContain("Stock verification rollback refused");
		(await database.ScalarAsync("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\";")).ShouldBe(migration);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockVerificationSessions\";")).ShouldBe("1");
	}

	private static async Task<string> StockVerificationMigrationAsync(LibraryMigrationPostgresFixture database)
	{
		await using var context = database.CreateContext();
		var migration = context.Database.GetMigrations().Single(name => name.EndsWith("_PersistLibraryStockVerification", System.StringComparison.Ordinal));
		await context.GetService<IMigrator>().MigrateAsync(migration);
		return migration;
	}

	private static async Task MigrateStockVerificationAsync(LibraryMigrationPostgresFixture database, string migration)
	{
		await using var context = database.CreateContext();
		await context.GetService<IMigrator>().MigrateAsync(migration);
	}

	private static string StockBaseline(string id, string tenant) => $$"""
		INSERT INTO "AppLibraryStockVerificationBaselines"
		 ("Id", "TenantId", "SessionId", "CopyId", "BookId", "AccessionNumber", "SectionId", "ShelfId", "CopyStatus")
		VALUES ('{{id}}', '{{tenant}}', '{{StockVerificationSessionId}}', '{{CatalogueOldCopy}}', '{{CatalogueOldBook}}', 'CAT-OLD-1',
		 'bd100000-0000-0000-0000-000000000006', 'bd100000-0000-0000-0000-000000000007', 0);
		""";
}
