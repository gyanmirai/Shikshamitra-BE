using System;
using System.Threading.Tasks;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Receiving_Postgres_Should_Accept_A_Complete_New_Batch_Inside_An_Ef_Style_Savepoint()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.StockReceiving);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var (copyId, _) = await SeedLoanContextAsync(factory);
		var batch = ReceivingSql(copyId).Replace("BEGIN;", "BEGIN; SAVEPOINT receiving_save;", StringComparison.Ordinal)
			.Replace("COMMIT;", "RELEASE SAVEPOINT receiving_save; COMMIT;", StringComparison.Ordinal);
		await database.ExecuteAsync(batch);
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryReceivedCopies\";")).ShouldBe("1");
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockReceiptCommands\";")).ShouldBe("1");
	}
}
