using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.ExternalReceivables;

public sealed class FinanceExternalPositionEvidenceSqlite_Tests
{
	[Fact]
	public async Task Consumer_Evidence_Sqlite_Bridge_Should_Keep_Counts_Aggregates_And_Pages_In_Sql()
	{
		await using var connection = await OpenAsync();
		await using var context = CreateContext(connection);
		await context.Database.ExecuteSqlRawAsync("CREATE TABLE Probes (Id INTEGER, Raw TEXT, ExpectedPayer TEXT)");
		await InsertAsync(connection, 1, "{\"Amount\":100.75,\"Payer\":{\"Id\":\"actual-id\"}}", "actual-id");
		await InsertAsync(connection, 2, "{\"Amount\":40.25,\"Payer\":{\"Id\":null}}", "null");
		await InsertAsync(connection, 3, "{bad", "null");
		await InsertAsync(connection, 4, "{\"Amount\":10,\"Payer\":{}}", "null");
		await InsertAsync(connection, 5, "{\"Amount\":1,\"Amount\":2,\"Payer\":{\"Id\":null}}", "null");
		await InsertAsync(connection, 6, "{\"Amount\":10,\"Payer\":{\"Id\":\"null\"}}", "null");
		var positions = context.Database.SqlQueryRaw<FinanceExternalPositionEvidencePostgres_Tests.PositionProbe>("""
			SELECT Id, "AppFinanceTryJsonMoney"(Raw, 'Amount') AS Amount,
			    "AppFinanceTryJsonScalar"(Raw, 'Payer.Id', 'string') AS PayerText,
			    "AppFinanceTryJsonScalar"(Raw, 'Payer.Id', 'null') AS JsonNullPayerText, ExpectedPayer FROM Probes
			""");
		var verified = positions.Where(row => row.Amount.HasValue &&
			(row.ExpectedPayer == "null" ? row.JsonNullPayerText == "null" : row.PayerText != null && row.PayerText == row.ExpectedPayer));
		(await positions.CountAsync()).ShouldBe(6);
		(await verified.CountAsync()).ShouldBe(2);
		(await verified.SumAsync(row => row.Amount)).ShouldBe(141m);
		(await verified.OrderBy(row => row.Id).Skip(1).Take(1).Select(row => row.Id).SingleAsync()).ShouldBe(2);
		(await verified.OrderBy(row => row.Id).Skip(2).Take(1).ToListAsync()).ShouldBeEmpty();
		verified.ToQueryString().ShouldContain("WHERE");
	}

	[Fact]
	public async Task Consumer_Evidence_Sqlite_Bridge_Should_Preserve_Full_Decimal_And_Propagate_Cancellation()
	{
		await using var connection = await OpenAsync();
		await using var context = CreateContext(connection);
		var document = "{\"Amount\":9999999999999999.99}";
		var query = context.Database.SqlQuery<decimal?>($"""
			SELECT "AppFinanceTryJsonMoney"({document}, 'Amount') AS "Value"
			""");
		(await query.SingleAsync()).ShouldBe(9999999999999999.99m);
		await Should.ThrowAsync<OperationCanceledException>(() => query.SingleAsync(new CancellationToken(true)));
		var unicode = "{\"Reason\":\"Maya José / ગુજરાતી\"}";
		(await context.Database.SqlQuery<string>($"""
			SELECT "AppFinanceTryJsonScalar"({unicode}, 'Reason', 'string') AS "Value"
			""").SingleAsync()).ShouldBe("Maya José / ગુજરાતી");
	}

	private static DbContext CreateContext(SqliteConnection connection) =>
		new(new DbContextOptionsBuilder().UseSqlite(connection).Options);

	private static async Task<SqliteConnection> OpenAsync()
	{
		var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync();
		connection.CreateFunction<string?, string?, string?, string?>("AppFinanceTryJsonScalar",
			FinanceExternalEvidenceJson.Scalar, isDeterministic: true);
		// SQLite REAL cannot represent the full supported decimal range exactly.
		connection.CreateFunction<string?, string?, string?>("AppFinanceTryJsonMoney",
			(document, path) => FinanceExternalEvidenceJson.Money(document, path)?.ToString("0.00", CultureInfo.InvariantCulture),
			isDeterministic: true);
		return connection;
	}

	private static async Task InsertAsync(SqliteConnection connection, int id, string document, string expectedPayer)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = "INSERT INTO Probes (Id, Raw, ExpectedPayer) VALUES ($id, $raw, $payer)";
		command.Parameters.AddWithValue("$id", id);
		command.Parameters.AddWithValue("$raw", document);
		command.Parameters.AddWithValue("$payer", expectedPayer);
		await command.ExecuteNonQueryAsync();
	}
}
