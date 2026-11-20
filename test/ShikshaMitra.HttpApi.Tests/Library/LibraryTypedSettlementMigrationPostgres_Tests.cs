using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

[Collection(LibraryCirculationMigrationCollection.Name)]
public partial class LibraryTypedSettlementMigrationPostgres_Tests(LibraryCirculationMigrationTemplate template)
{
	[LibraryPostgresFact]
	public async Task Complete_Migration_History_Should_Reach_The_Current_Model_Without_Drift()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateFullHistoryAsync(LibraryMigrationPostgresFixture.TypedSettlement);
		(await database.ScalarAsync("SELECT max(\"MigrationId\") FROM \"__EFMigrationsHistory\";")).ShouldBe(LibraryMigrationPostgresFixture.TypedSettlement);
		using var context = database.CreateContext();
		context.Database.HasPendingModelChanges().ShouldBeFalse();
		(await database.ScalarAsync("SELECT data_type FROM information_schema.columns WHERE table_name='Finance_ChargeCollectionRecords' AND column_name='Amount';")).ShouldBe("numeric");
	}

	[LibraryPostgresTheory]
	[InlineData("0.1", "0.10")]
	[InlineData("100.75", "100.75")]
	[InlineData("9999999999999998", "9999999999999998.00")]
	public async Task Upgrade_Should_Preserve_Exact_Legacy_Money_Without_The_Session_Float_Text_Setting_Changing_Its_Value(string original, string expected)
	{
		await using var database = await CreateM4Async();
		await database.ExecuteAsync(MoneyRows(original));
		await UpgradeAsync(database, "SET extra_float_digits=-15;\n");
		(await database.ScalarAsync("SELECT \"TotalPaidFee\"::text FROM \"Finance_FeeRecordMetadatas\";")).ShouldBe(expected);
		(await database.ScalarAsync("SELECT \"Amount\"::text FROM \"Finance_ChargeCollectionRecords\";")).ShouldBe(expected);
		(await database.ScalarAsync("SELECT \"Currency\" FROM \"Finance_FeeRecordMetadatas\";")).ShouldBeNull();
		(await database.ScalarAsync("SELECT \"StudentId\"::text FROM \"Finance_FeeRecordMetadatas\";")).ShouldBe("84000000-0000-0000-0000-000000000001");
	}

	[LibraryPostgresTheory]
	[InlineData("0.30000000000000004")]
	[InlineData("1.005")]
	[InlineData("NaN")]
	[InlineData("Infinity")]
	[InlineData("-Infinity")]
	[InlineData("-1")]
	[InlineData("10000000000000000")]
	public async Task Unsupported_Legacy_Money_Should_Stop_Atomically_With_An_Explicit_Review_Error(string original)
	{
		await using var database = await CreateM4Async();
		await database.ExecuteAsync(MoneyRows(original));
		var error = await Should.ThrowAsync<PostgresException>(() => UpgradeAsync(database));
		error.MessageText.ShouldContain("money precision review");
		(await database.ScalarAsync("SELECT data_type FROM information_schema.columns WHERE table_name='Finance_FeeRecordMetadatas' AND column_name='TotalPaidFee';")).ShouldBe("double precision");
		(await database.ScalarAsync("SELECT count(*) FROM information_schema.columns WHERE table_name='Finance_FeeRecordMetadatas' AND column_name='EmployeeId';")).ShouldBe("0");
		(await database.ScalarAsync("SELECT count(*) FROM \"Finance_ChargeCollectionRecords\";")).ShouldBe("1");
	}

	[LibraryPostgresFact]
	public async Task Safe_Rollback_Should_Preserve_Original_Money_And_No_Fabricated_Payer_Defaults()
	{
		await using var database = await CreateM4Async();
		await database.ExecuteAsync(MoneyRows("100.75"));
		await UpgradeAsync(database);
		await RollbackAsync(database);
		(await database.ScalarAsync("SELECT \"TotalPaidFee\"::text FROM \"Finance_FeeRecordMetadatas\";")).ShouldBe("100.75");
		(await database.ScalarAsync("SELECT column_default FROM information_schema.columns WHERE table_name='Finance_ChargeCollectionRecords' AND column_name='StudentId';")).ShouldBeNull();
		(await database.ScalarAsync("SELECT column_default FROM information_schema.columns WHERE table_name='Finance_ChargeWaiverApplications' AND column_name='StudentId';")).ShouldBeNull();
		(await database.ScalarAsync("SELECT column_default FROM information_schema.columns WHERE table_name='Finance_FeePaymentReceipts' AND column_name='BoardId';")).ShouldBeNull();
	}

	[LibraryPostgresTheory]
	[InlineData("UPDATE \"AppBookIssues\" SET \"BorrowerScopeProvenance\"=1;")]
	[InlineData("UPDATE \"Finance_FeeRecordMetadatas\" SET \"Currency\"='INR';")]
	[InlineData("UPDATE \"Finance_ChargeCollectionRecords\" SET \"Amount\"=99999999999999.99;")]
	public async Task Rollback_Should_Refuse_To_Erase_Confirmed_Context_Or_Unrepresentable_Money(string newEvidence)
	{
		await using var database = await CreateM4Async();
		await database.ExecuteAsync(MoneyRows("100.75"));
		await UpgradeAsync(database);
		await database.ExecuteAsync(newEvidence);
		var error = await Should.ThrowAsync<PostgresException>(() => RollbackAsync(database));
		error.MessageText.ShouldContain("cannot preserve");
		(await database.ScalarAsync("SELECT data_type FROM information_schema.columns WHERE table_name='Finance_ChargeCollectionRecords' AND column_name='Amount';")).ShouldBe("numeric");
	}

	private async Task<LibraryMigrationPostgresFixture> CreateM4Async()
	{
		var database = await template.CloneAsync();
		try
		{
			await database.ExecuteAsync(database.Script(LibraryMigrationPostgresFixture.CopyEvidence, LibraryMigrationPostgresFixture.CirculationEvidence));
			return database;
		}
		catch { await database.DisposeAsync(); throw; }
	}
	private static Task UpgradeAsync(LibraryMigrationPostgresFixture database, string prefix = "") => database.ExecuteAsync(prefix +
		database.Script(LibraryMigrationPostgresFixture.CirculationEvidence, LibraryMigrationPostgresFixture.TypedSettlement));
	private static Task RollbackAsync(LibraryMigrationPostgresFixture database) => database.ExecuteAsync(
		database.Script(LibraryMigrationPostgresFixture.TypedSettlement, LibraryMigrationPostgresFixture.CirculationEvidence));

	// Precision witnesses use the original recorded payer ID; they do not create a person or post a payment.
	private static string MoneyRows(string amount) => $$"""
		INSERT INTO "Finance_FeeRecordMetadatas" ("Id","TenantId","StudentId","AcademicYearId","TotalPaidFee","ReceiptNumber","Status","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('a1000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','84000000-0000-0000-0000-000000000001','81000000-0000-0000-0000-000000000001','{{amount}}'::double precision,'PRECISION-WITNESS',1,'{}','legacy-money','2026-11-17T03:00:00Z');
		INSERT INTO "Finance_ChargeCollectionRecords" ("Id","TenantId","StudentId","ChargeId","Amount","PaymentDate","FeeRecordMetadataId","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('a2000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000001','84000000-0000-0000-0000-000000000001','a3000000-0000-0000-0000-000000000001','{{amount}}'::double precision,'2026-11-17T03:00:00Z','a1000000-0000-0000-0000-000000000001','{}','legacy-allocation','2026-11-17T03:00:00Z');
		""";
}
