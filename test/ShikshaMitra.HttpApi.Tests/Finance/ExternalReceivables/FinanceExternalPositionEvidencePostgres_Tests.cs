using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Library;
using Shouldly;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalPositionEvidencePostgres_Tests
{
	private const string Migration = "20261118195300_AddFinanceExternalPositionEvidenceFunctions";

	[LibraryPostgresFact]
	public async Task Consumer_Evidence_Functions_Should_Guard_Counts_And_Pages_With_Invalid_Siblings()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateAsync();
		await database.ExecuteAsync(database.Script(LibraryMigrationPostgresFixture.CataloguePrice, Migration));
		await using var context = database.CreateContext();
		var positions = context.Database.SqlQueryRaw<PositionProbe>(EscapeSqlFormat(ProbeSql));
		var verified = positions.Where(row => row.Amount.HasValue &&
			(row.ExpectedPayer == "null" ? row.JsonNullPayerText == "null" : row.PayerText != null && row.PayerText == row.ExpectedPayer));
		(await positions.CountAsync()).ShouldBe(10);
		(await verified.CountAsync()).ShouldBe(2);
		(await verified.SumAsync(row => row.Amount)).ShouldBe(141m);
		(await verified.OrderBy(row => row.Id).Skip(1).Take(1).Select(row => row.Id).SingleAsync()).ShouldBe(2);
		(await verified.OrderBy(row => row.Id).Skip(2).Take(1).ToListAsync()).ShouldBeEmpty();
	}

	[LibraryPostgresFact]
	public async Task Consumer_Evidence_Functions_Should_Preserve_Exact_Money_Nulls_And_Unicode()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateAsync();
		await database.ExecuteAsync(database.Script(LibraryMigrationPostgresFixture.CataloguePrice, Migration));
		await using var context = database.CreateContext();
		var rows = await context.Database.SqlQueryRaw<MoneyProbe>(EscapeSqlFormat(MoneySql)).OrderBy(row => row.Id).ToListAsync();
		rows[0].Amount.ShouldBe(9999999999999999.99m);
		rows[1].Amount.ShouldBe(12.5m);
		rows.Skip(2).ShouldAllBe(row => row.Amount == null);
		(await database.ScalarAsync("""SELECT "AppFinanceTryJsonScalar"('{"Reason":"Maya José / ગુજરાતી"}', 'Reason', 'string')"""))
			.ShouldBe("Maya José / ગુજરાતી");
		(await database.ScalarAsync("""SELECT "AppFinanceTryJsonScalar"('{"Payer":{"Id":null}}', 'Payer.Id', 'null')"""))
			.ShouldBe("null");
		(await database.ScalarAsync("""SELECT "AppFinanceTryJsonScalar"('{"Payer":{}}', 'Payer.Id', 'null')"""))
			.ShouldBeNull();
		(await database.ScalarAsync("""SELECT "AppFinanceTryJsonScalar"('{"Reason":"\u0000"}', 'Reason', 'string')"""))
			.ShouldBeNull();
	}

	[LibraryPostgresFact]
	public async Task Consumer_Evidence_Functions_Migration_Should_Reverse_Without_Changing_Stored_Rows()
	{
		await using var database = await LibraryMigrationPostgresFixture.CreateAsync();
		var original = await database.ScalarAsync("""SELECT jsonb_agg(to_jsonb(row) ORDER BY "Id")::text FROM "AppBooks" row""");
		var up = database.Script(LibraryMigrationPostgresFixture.CataloguePrice, Migration);
		await database.ExecuteAsync(up);
		(await database.ScalarAsync(InstalledFunctionCount)).ShouldBe("2");
		await database.ExecuteAsync(database.Script(Migration, LibraryMigrationPostgresFixture.CataloguePrice));
		(await database.ScalarAsync(InstalledFunctionCount)).ShouldBe("0");
		await database.ExecuteAsync(up);
		(await database.ScalarAsync(InstalledFunctionCount)).ShouldBe("2");
		(await database.ScalarAsync("""SELECT jsonb_agg(to_jsonb(row) ORDER BY "Id")::text FROM "AppBooks" row"""))
			.ShouldBe(original);
	}

	private const string InstalledFunctionCount = """
		SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
		WHERE n.nspname='public' AND p.proname IN ('AppFinanceTryJsonScalar','AppFinanceTryJsonMoney')
		""";

	// EF formats raw SQL placeholders even for this fixed, parameter-free probe.
	private static string EscapeSqlFormat(string sql) => sql.Replace("{", "{{", StringComparison.Ordinal)
		.Replace("}", "}}", StringComparison.Ordinal);

	private const string ProbeSql = """
		SELECT c."Id", "AppFinanceTryJsonMoney"(c."Raw", 'Amount') AS "Amount",
			"AppFinanceTryJsonScalar"(c."Raw", 'Payer.Id', 'string') AS "PayerText",
			"AppFinanceTryJsonScalar"(c."Raw", 'Payer.Id', 'null') AS "JsonNullPayerText", c."ExpectedPayer"
		FROM (VALUES
			(1, '{"Amount":100.75,"Payer":{"Id":"a0000000-0000-0000-0000-000000000001"}}', 'a0000000-0000-0000-0000-000000000001'),
			(2, '{"Amount":40.25,"Payer":{"Id":null}}', 'null'),
			(3, '{bad', 'null'),
			(4, '{"Amount":"10","Payer":{"Id":null}}', 'null'),
			(5, '{"Amount":1,"Amount":2,"Payer":{"Id":null}}', 'null'),
			(6, '{"Amount":10,"Payer":[]}', 'null'),
			(7, '{"Amount":10,"Payer":{}}', 'null'),
			(8, '{"Amount":10,"Payer":{"Id":null},"Payer":{"Id":null}}', 'null'),
			(9, '{"Amount":10,"Payer":{"Id":42}}', 'null'),
			(10, '{"Amount":10,"Payer":{"Id":"null"}}', 'null')
		) AS c("Id", "Raw", "ExpectedPayer")
		""";

	private const string MoneySql = """
		SELECT c."Id", "AppFinanceTryJsonMoney"(c."Raw", 'Amount') AS "Amount"
		FROM (VALUES (1, '{"Amount":9999999999999999.99}'), (2, '{"Amount":12.5000}'),
			(3, '{"Amount":0.001}'), (4, '{"Amount":10000000000000000}'),
			(5, '{"Amount":-1}'), (6, '{"Amount":1e1}'), (7, '{"Amount":null}')) AS c("Id", "Raw")
		""";

	public sealed class PositionProbe
	{
		public int Id { get; set; }
		public decimal? Amount { get; set; }
		public string? PayerText { get; set; }
		public string? JsonNullPayerText { get; set; }
		public string ExpectedPayer { get; set; } = string.Empty;
	}

	public sealed class MoneyProbe
	{
		public int Id { get; set; }
		public decimal? Amount { get; set; }
	}
}
