using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	[LibraryPostgresFact]
	public async Task Receiving_Postgres_Should_Not_Append_An_Extra_Copy_To_An_Old_Sealed_Line()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.StockReceiving);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var (copyId, _) = await SeedLoanContextAsync(factory);
		await database.ExecuteAsync(ReceivingSql(copyId));
		await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync($$"""
			BEGIN;
			INSERT INTO "AppBookCopies" ("Id","TenantId","BookId","CopyNumber","AccessionNumber","SectionId","ShelfId","Status","Remarks")
			SELECT 'fae00000-0000-0000-0000-000000000006',"TenantId","BookId","CopyNumber"+1,'RECV-LATE',"SectionId","ShelfId",0,NULL
			FROM "AppBookCopies" WHERE "Id"='{{ReceivingCopy}}';
			INSERT INTO "AppLibraryReceivedCopies" ("Id","TenantId","ReceiptLineId","CopyId","AccessionNumber","CopyNumber")
			SELECT 'fae00000-0000-0000-0000-000000000007',"TenantId",'{{ReceivingLine}}',"Id","AccessionNumber","CopyNumber"
			FROM "AppBookCopies" WHERE "Id"='fae00000-0000-0000-0000-000000000006';
			COMMIT;
			"""));
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryReceivedCopies\";")).ShouldBe("1");
	}

	[LibraryPostgresFact]
	public async Task Empty_Receiving_Postgres_Rollback_And_Reapply_Should_Preserve_Ordinary_Preexisting_Stock()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.StockReceiving);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var (copyId, _) = await SeedLoanContextAsync(factory);
		await database.RollbackLibrarySchemaAsync(LibraryMigrationPostgresFixture.TypedSettlement);
		(await database.ScalarAsync($"SELECT count(*) FROM \"AppBookCopies\" WHERE \"Id\"='{copyId:D}';")).ShouldBe("1");
		await database.ExecuteAsync(database.Script(LibraryMigrationPostgresFixture.TypedSettlement, LibraryMigrationPostgresFixture.StockReceiving));
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockReceipts\";")).ShouldBe("0");
		(await database.ScalarAsync($"SELECT count(*) FROM \"AppBookCopies\" WHERE \"Id\"='{copyId:D}';")).ShouldBe("1");
	}

	[LibraryPostgresTheory]
	[InlineData("AppLibraryStockReceipts", "\"SourceName\"='Rewritten donor'")]
	[InlineData("AppLibraryStockReceiptLines", "\"UnitCost\"=10.25,\"Currency\"='INR'")]
	[InlineData("AppLibraryReceivedCopies", "\"AccessionNumber\"='REWRITTEN'")]
	[InlineData("AppLibraryStockReceiptCommands", "\"IdempotencyKey\"='rewritten-key'")]
	public async Task Receiving_Postgres_Should_Protect_Each_Committed_Evidence_Family_From_Updates(string table, string change)
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.StockReceiving);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var (copyId, _) = await SeedLoanContextAsync(factory);
		await database.ExecuteAsync(ReceivingSql(copyId));
		await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync($"UPDATE \"{table}\" SET {change};"));
	}

	[LibraryPostgresFact]
	public async Task Receiving_Postgres_Should_Not_Truncate_Committed_Provenance()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.StockReceiving);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var (copyId, _) = await SeedLoanContextAsync(factory);
		await database.ExecuteAsync(ReceivingSql(copyId));
		await Should.ThrowAsync<PostgresException>(() => database.ExecuteAsync("TRUNCATE \"AppLibraryStockReceipts\" CASCADE;"));
	}

	[LibraryPostgresFact]
	public async Task Receiving_Postgres_Should_Refuse_Rollback_That_Would_Erase_Acquisition_Evidence()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.StockReceiving);
		await using var factory = new LibraryPostgresApiTestFactory(database);
		var (copyId, _) = await SeedLoanContextAsync(factory);
		await database.ExecuteAsync(ReceivingSql(copyId));
		await Should.ThrowAsync<PostgresException>(() => database.RollbackLibrarySchemaAsync(LibraryMigrationPostgresFixture.TypedSettlement));
		(await database.ScalarAsync("SELECT count(*) FROM \"AppLibraryStockReceipts\";")).ShouldBe("1");
	}
}
