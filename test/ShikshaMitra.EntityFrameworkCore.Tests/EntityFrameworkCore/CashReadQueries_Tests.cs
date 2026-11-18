using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Accounts.CashManagement;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public class CashReadQueries_Tests
{
	[Fact]
	public void PostgreSql_summary_is_one_aggregate_row_without_cash_history()
	{
		using var db = Database();
		var tenant = Guid.NewGuid();
		var year = Guid.NewGuid();
		var scoped = db.Set<CashCollection>().IgnoreQueryFilters().Where(x => x.TenantId == tenant && x.AcademicYearId == year);
		var sql = CashReadQueries.SummaryQuery(scoped).ToQueryString();
		sql.ShouldContain("sum(");
		sql.ShouldContain("CASE");
		sql.ShouldContain("AcademicYearId");
		sql.ShouldContain("TenantId");
		foreach (var history in new[] { "Finance_CashCommands", "Finance_CashDeposits", "Finance_CashExpenses", "ResultJson" })
			sql.ShouldNotContain(history);
	}

	[Fact]
	public void PostgreSql_month_and_activity_dates_stay_in_SQL_and_legacy_credits_are_not_double_counted()
	{
		using var db = Database();
		var sources = db.Set<CashCollection>().IgnoreQueryFilters();
		var start = new DateTimeOffset(2026, 7, 31, 18, 30, 0, TimeSpan.Zero);
		var end = start.AddMonths(1);
		var monthSql = CashReadQueries.CollectionPeriodQuery(sources, start, end).Select(x => x.Id).ToQueryString();
		monthSql.ShouldContain("CollectedAt");
		monthSql.ShouldContain(">=");
		monthSql.ShouldContain("<");
		monthSql.ShouldNotContain("ANY (");
		var creditSql = CashReadQueries.CreditAmountsQuery(sources, start, end).ToQueryString();
		creditSql.ShouldContain("UNION ALL");
		creditSql.ShouldContain("NOT EXISTS");
		creditSql.ShouldContain("CreditDate");
		creditSql.ShouldContain(">=");
		creditSql.ShouldContain("<");
		creditSql.ShouldNotContain("ResultJson");
		creditSql.ShouldNotContain("Finance_CashExpenses");
	}

	[Fact]
	public void PostgreSql_page_projects_only_bounded_rows_and_bank_metadata_and_options_project_no_history()
	{
		using var db = Database();
		var query = db.Set<CashCollection>().IgnoreQueryFilters().Where(x => x.AcademicYearId == Guid.Empty);
		var pageSql = CashManagementReadProjection.Rows(query.OrderBy(x => x.Id).Skip(40).Take(20)).ToQueryString();
		pageSql.ShouldContain("LIMIT");
		pageSql.ShouldContain("OFFSET");
		pageSql.ShouldContain("Finance_CashDeposits");
		pageSql.ShouldContain("DepositDate");
		foreach (var history in new[] { "Finance_CashCommands", "Finance_CashExpenses", "Finance_CashDepositCredits", "ResultJson", "CreditedAmount" })
			pageSql.ShouldNotContain(history);
		var optionsSql = CashManagementReadProjection.Sources(query).ToQueryString();
		foreach (var history in new[] { "Finance_CashCommands", "Finance_CashDeposits", "Finance_CashExpenses", "ExtraProperties", "Notes" })
			optionsSql.ShouldNotContain(history);
		optionsSql.ShouldNotContain("LIMIT");
		optionsSql.ShouldContain("ConcurrencyStamp");
	}

	private static ShikshaMitraDbContext Database()
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		return new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Port=1;Database=cash_read_offline;Username=validation;Password=unused;Timeout=1").Options);
	}
}
