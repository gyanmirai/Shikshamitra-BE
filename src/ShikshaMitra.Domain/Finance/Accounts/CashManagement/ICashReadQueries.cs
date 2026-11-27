using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

/// <summary>Provider-aware reads over an already authorized cash source query.</summary>
public interface ICashReadQueries
{
	Task<CashBalanceFacts> SummarizeAsync(IQueryable<CashCollection> query);
	Task<CashActivityFacts> SummarizeActivityAsync(IQueryable<CashCollection> query, DateTimeOffset? start, DateTimeOffset? end);
	Task<IQueryable<CashCollection>> FilterCollectionPeriodAsync(IQueryable<CashCollection> query, DateTimeOffset start, DateTimeOffset end);
	Task<(IReadOnlyList<CashExpenseFact> Items, int TotalCount)> ReadExpensesAsync(IQueryable<CashCollection> query,
		DateTimeOffset? start, DateTimeOffset? end, int skip, int take);
}

public sealed class CashBalanceFacts
{
	public decimal Collected { get; set; }
	public decimal PendingApproval { get; set; }
	public decimal Approved { get; set; }
	public decimal Spent { get; set; }
	public decimal Deposited { get; set; }
	public decimal InTransit { get; set; }
	public decimal Remaining { get; set; }
}

public sealed record CashActivityFacts(decimal Collected, decimal Spent, decimal Deposited);
public sealed record CashExpenseFact(Guid Id, string Name, decimal Amount, DateTimeOffset PaymentDate, string SourceReference);
