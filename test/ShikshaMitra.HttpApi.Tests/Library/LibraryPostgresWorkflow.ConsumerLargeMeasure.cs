using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using Shouldly;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private Task<FinanceConsumerLargeMeasurement> MeasureFinancePgConsumerLargeAsync(LibraryPostgresApiTestFactory factory,
		int population) => InFinancePgAsync(factory, async services =>
	{
		using var principal = ConsumerPgPrincipal(services);
		var reader = await ConsumerPgReaderAsync(services);
		var db = await FinancePgDbAsync(services);
		// Keep the ordinary production command budget. No enlarged timeout for this gate.
		db.Database.SetCommandTimeout(30);
		await db.Database.ExecuteSqlRawAsync("SET LOCAL DateStyle = 'SQL, DMY'");
		using var activity = new Activity("Native large canonical consumer").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var capture = new FinanceConsumerPgSqlCapture(activity.TraceId, captureAnySourceOrFinance: true);
		var query = await reader.GetQueryAsync(new(LibraryChargePositionReadPurpose.ChargeHistory, factory.YearId));
		capture.Commands.ShouldBeEmpty();
		async Task<T> Measure<T>(string operation, Func<Task<T>> execute)
		{
			var watch = Stopwatch.StartNew();
			consumerOutput.WriteLine($"Population {population}: begin {operation}; command timeout 30 seconds.");
			try { return await execute(); }
			catch
			{
				// Preserve the actual failed SQL/parameters without rerunning a canceled
				// command or hiding the provider failure in an unavailable/zero result.
				foreach (var command in capture.Commands)
					consumerOutput.WriteLine(JsonSerializer.Serialize(new
					{
						Operation = operation, SqlLength = command.Sql.Length, Sql = command.Sql,
						Parameters = command.Parameters.Select(parameter => new
						{
							parameter.ParameterName, Type = parameter.NpgsqlDbType.ToString(),
							Value = parameter.Value == DBNull.Value ? null : parameter.Value
						})
					}));
				throw;
			}
			finally { consumerOutput.WriteLine($"Population {population}: {operation} elapsed {watch.Elapsed.TotalMilliseconds:F3} ms."); }
		}
		(await Measure("source count", () => query.CountAsync())).ShouldBe(population);
		var verified = query.Where(row => row.Evidence == FinanceExternalPositionEvidence.Verified);
		(await Measure("verified count", () => verified.CountAsync())).ShouldBe(population);
		(await Measure("collectible count", () => verified.CountAsync(row => row.Collectible > 0m))).ShouldBe(population);
		var summary = await Measure("grouped eight monetary totals", () => verified.GroupBy(row => row.Currency)
			.Select(group => new FinanceConsumerLargeMoney
			{
				Currency = group.Key, Original = group.Sum(row => row.Original ?? 0m), Accepted = group.Sum(row => row.Accepted ?? 0m),
				Settled = group.Sum(row => row.Settled ?? 0m), Pending = group.Sum(row => row.Pending ?? 0m),
				Waived = group.Sum(row => row.NetWaived ?? 0m), Outstanding = group.Sum(row => row.FinancialOutstanding ?? 0m),
				Collectible = group.Sum(row => row.Collectible ?? 0m), Refunded = group.Sum(row => row.Refunded ?? 0m)
			}).SingleAsync());
		summary.Currency.ShouldBe("INR"); summary.Original.ShouldBe(population * 100.75m);
		summary.Accepted.ShouldBe(0m); summary.Settled.ShouldBe(0m); summary.Pending.ShouldBe(0m);
		summary.Waived.ShouldBe(0m); summary.Refunded.ShouldBe(0m);
		summary.Outstanding.ShouldBe(population * 100.75m); summary.Collectible.ShouldBe(population * 100.75m);
		var ids = verified.Select(row => row.SourceId);
		var joined = from assessment in db.Set<LibraryCharge>() join id in ids on assessment.Id equals id
			where assessment.TenantId == FinancePgTenantId && assessment.AcademicYearId == factory.YearId select assessment.Id;
		(await Measure("scoped source join count", () => joined.CountAsync())).ShouldBe(population);
		var firstPage = await Measure("first bounded page", () => joined.OrderBy(id => id).Take(25).ToListAsync());
		var middlePage = await Measure("middle bounded page", () => joined.OrderBy(id => id).Skip(population / 2).Take(25).ToListAsync());
		firstPage.Count.ShouldBe(25); middlePage.Count.ShouldBe(25);
		firstPage.Distinct().Count().ShouldBe(25); middlePage.Intersect(firstPage).ShouldBeEmpty();
		(await Measure("empty terminal page", () => joined.OrderBy(id => id).Skip(population).Take(25).ToListAsync())).ShouldBeEmpty();
		var commands = capture.Commands.ToArray();
		commands.Length.ShouldBe(8);
		await ExplainConsumerPgAsync(db, commands, $"large-source-count-{population}");
		return new FinanceConsumerLargeMeasurement(commands.Length, commands.Select(command => command.Sql.Length).ToArray());
	});

	private sealed record FinanceConsumerLargeMeasurement(int Commands, int[] SqlLengths);
	private sealed class FinanceConsumerLargeMoney
	{
		public string? Currency { get; init; }
		public decimal Original { get; init; }
		public decimal Accepted { get; init; }
		public decimal Settled { get; init; }
		public decimal Pending { get; init; }
		public decimal Waived { get; init; }
		public decimal Outstanding { get; init; }
		public decimal Collectible { get; init; }
		public decimal Refunded { get; init; }
	}
}
