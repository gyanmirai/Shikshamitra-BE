using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresTheory]
	[InlineData("MissingCommand")]
	[InlineData("WrongCommandActor")]
	[InlineData("MissingLine")]
	[InlineData("MissingCopy")]
	[InlineData("ForeignSourceTenant")]
	public async Task Receiving_Postgres_Should_Reject_Incomplete_Or_Contradictory_Committed_Evidence(string defect)
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.StockReceiving);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var (copyId, _) = await SeedLoanContextAsync(factory);
		var sourceTenant = await database.ScalarAsync($"SELECT \"TenantId\"::text FROM \"AppBookCopies\" WHERE \"Id\"='{copyId:D}';");
		sourceTenant.ShouldBe(Infrastructure.Auth.TestAuthHandler.TestTenantId);
		await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(ReceivingSql(copyId, defect)));
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockReceipts\";")).ShouldBe("0");
	}

	[LibraryPostgresTheory]
	[InlineData("NULL", "unknown")]
	[InlineData("0.00", "0.00")]
	[InlineData("9999999999999998.99", "9999999999999998.99")]
	public async Task Receiving_Postgres_Should_Preserve_Unknown_Zero_And_Full_Supported_Paise_Without_Backfilling_Old_Copies(string cost, string expected)
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.StockReceiving);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var (copyId, _) = await SeedLoanContextAsync(factory);
		await MigrateStockVerificationAsync(database, LibraryMigrationPostgresFixture.StockReceiving);
		await database.ExecuteAsync(ReceivingSql(copyId, cost: cost));
		(await database.ScalarAsync("SELECT COALESCE(\"UnitCost\"::text,'unknown') FROM \"AppLibraryStockReceiptLines\";")).ShouldBe(expected);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryReceivedCopies\";")).ShouldBe("1");
		(await database.ScalarAsync($"SELECT count(*) FROM \"AppLibraryReceivedCopies\" WHERE \"CopyId\"='{copyId:D}';")).ShouldBe("0");
		(await database.ScalarAsync("SELECT \"ReceivedOn\"::text FROM \"AppLibraryStockReceipts\";")).ShouldBe("2026-11-18");
		(await database.ScalarAsync("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\";")).ShouldBe(LibraryMigrationPostgresFixture.StockReceiving);
	}

	[LibraryPostgresTheory]
	[InlineData("0.001")]
	[InlineData("10000000000000000.00")]
	public async Task Receiving_Postgres_Should_Reject_Subcent_And_Overflow_Cost_Instead_Of_Rounding_The_Input(string cost)
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.StockReceiving);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var (copyId, _) = await SeedLoanContextAsync(factory);
		await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync(ReceivingSql(copyId, cost: cost)));
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockReceipts\";")).ShouldBe("0");
	}
}
