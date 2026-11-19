using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public sealed class CashReadQueries(IDbContextProvider<ShikshaMitraDbContext> contexts)
	: ICashReadQueries, ITransientDependency
{
	public static IQueryable<CashBalanceFacts> SummaryQuery(IQueryable<CashCollection> query) =>
		query.GroupBy(x => 1).Select(group => new CashBalanceFacts
		{
			Collected = group.Sum(x => x.Amount),
			PendingApproval = group.Sum(x => x.Status == 0 ? x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount : 0m),
			Approved = group.Sum(x => x.Status == 1 ? x.Amount : 0m),
			Spent = group.Sum(x => x.SpentAmount),
			Deposited = group.Sum(x => x.DepositedAmount),
			InTransit = group.Sum(x => x.InTransitAmount),
			Remaining = group.Sum(x => x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount)
		});

	public async Task<CashBalanceFacts> SummarizeAsync(IQueryable<CashCollection> query)
	{
		if ((await contexts.GetDbContextAsync()).Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite")
			return await SummaryQuery(query).FirstOrDefaultAsync() ?? new CashBalanceFacts();
		// SQLite cannot aggregate decimal accurately in SQL. Keep this fallback restricted
		// to that provider and project scalar facts only; PostgreSQL always aggregates above.
		var rows = await query.Select(x => new { x.Amount, x.Status, x.SpentAmount, x.DepositedAmount, x.InTransitAmount }).ToListAsync();
		return new CashBalanceFacts
		{
			Collected = rows.Sum(x => x.Amount),
			PendingApproval = rows.Where(x => x.Status == 0).Sum(x => x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount),
			Approved = rows.Where(x => x.Status == 1).Sum(x => x.Amount),
			Spent = rows.Sum(x => x.SpentAmount), Deposited = rows.Sum(x => x.DepositedAmount),
			InTransit = rows.Sum(x => x.InTransitAmount),
			Remaining = rows.Sum(x => x.Amount - x.SpentAmount - x.DepositedAmount - x.InTransitAmount)
		};
	}

	public static IQueryable<CashCollection> CollectionPeriodQuery(IQueryable<CashCollection> query, DateTimeOffset start, DateTimeOffset end) =>
		query.Where(x => x.CollectedAt >= start && x.CollectedAt < end);

	public async Task<IQueryable<CashCollection>> FilterCollectionPeriodAsync(IQueryable<CashCollection> query, DateTimeOffset start, DateTimeOffset end)
	{
		if ((await contexts.GetDbContextAsync()).Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite") return CollectionPeriodQuery(query, start, end);
		// SQLite also cannot compare DateTimeOffset inequalities; keep counts/paging in SQL.
		var dates = await query.Select(x => new { x.Id, x.CollectedAt }).ToListAsync();
		var ids = dates.Where(x => x.CollectedAt >= start && x.CollectedAt < end).Select(x => x.Id).ToArray();
		return query.Where(x => Enumerable.Contains(ids, x.Id));
	}

	public static IQueryable<decimal> CreditAmountsQuery(IQueryable<CashCollection> query, DateTimeOffset? start, DateTimeOffset? end)
	{
		var deposits = query.SelectMany(x => x.Deposits);
		var credits = deposits.SelectMany(x => x.Credits);
		var legacy = deposits.Where(x => !x.Credits.Any() && x.CreditedAmount > 0m);
		if (start.HasValue && end.HasValue)
		{
			credits = credits.Where(x => x.CreditDate >= start.Value && x.CreditDate < end.Value);
			legacy = legacy.Where(x => x.CreditDate.HasValue && x.CreditDate.Value >= start.Value && x.CreditDate.Value < end.Value);
		}
		return credits.Select(x => x.Amount).Concat(legacy.Select(x => x.CreditedAmount));
	}

	public async Task<CashActivityFacts> SummarizeActivityAsync(IQueryable<CashCollection> query, DateTimeOffset? start, DateTimeOffset? end)
	{
		if ((await contexts.GetDbContextAsync()).Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite") return await SummarizeSqliteActivityAsync(query, start, end);
		var collections = start.HasValue && end.HasValue ? CollectionPeriodQuery(query, start.Value, end.Value) : query;
		var expenses = query.SelectMany(x => x.Expenses);
		if (start.HasValue && end.HasValue)
			expenses = expenses.Where(x => x.PaymentDate >= start.Value && x.PaymentDate < end.Value);
		return new CashActivityFacts(await collections.SumAsync(x => x.Amount), await expenses.SumAsync(x => x.Amount),
			await CreditAmountsQuery(query, start, end).SumAsync());
	}

	private static async Task<CashActivityFacts> SummarizeSqliteActivityAsync(IQueryable<CashCollection> query, DateTimeOffset? start, DateTimeOffset? end)
	{
		bool InPeriod(DateTimeOffset date) => !start.HasValue || date >= start.Value && date < end!.Value;
		var collections = await query.Select(x => new { x.Amount, x.CollectedAt }).ToListAsync();
		var expenses = await query.SelectMany(x => x.Expenses).Select(x => new { x.Amount, x.PaymentDate }).ToListAsync();
		var deposits = query.SelectMany(x => x.Deposits);
		var events = deposits.SelectMany(x => x.Credits).Select(x => new { x.Amount, CreditDate = (DateTimeOffset?)x.CreditDate });
		var legacy = deposits.Where(x => !x.Credits.Any() && x.CreditedAmount > 0m).Select(x => new { Amount = x.CreditedAmount, x.CreditDate });
		var credits = await events.Concat(legacy).ToListAsync();
		return new CashActivityFacts(collections.Where(x => InPeriod(x.CollectedAt)).Sum(x => x.Amount),
			expenses.Where(x => InPeriod(x.PaymentDate)).Sum(x => x.Amount),
			credits.Where(x => !start.HasValue || x.CreditDate.HasValue && InPeriod(x.CreditDate.Value)).Sum(x => x.Amount));
	}
}
