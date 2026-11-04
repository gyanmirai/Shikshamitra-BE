using System;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public partial class CashManagementAppService
{
	private async Task<CashActivitySummary> SummarizeActivityAsync(IQueryable<CashCollection> query, CashQuery input)
	{
		var year = await years.GetAsync(input.AcademicYearId);
		var period = AccountsRegisterPeriod.Resolve(year.StartDate, year.EndDate, input.Month, input.Year);
		var start = new DateTimeOffset(period.StartUtc);
		var end = new DateTimeOffset(period.EndUtc);
		bool InPeriod(DateTimeOffset date) => !input.Month.HasValue || date >= start && date < end;

		// Materialize only authorized monetary/date facts. SQLite cannot compare DateTimeOffset
		// inequalities; applying the exact India-calendar boundary here keeps providers consistent.
		var collections = await AsyncExecuter.ToListAsync(query.Select(x => new { x.Amount, x.CollectedAt }));
		var expenses = await AsyncExecuter.ToListAsync(query.SelectMany(x => x.Expenses)
			.Select(x => new { x.Amount, x.PaymentDate }));
		// A legacy deposit can be credited in several months. Each immutable credit owns its date;
		// the deposit's aggregate CreditedAmount/last CreditDate would move old activity forward.
		var creditEvents = query.SelectMany(x => x.Deposits).SelectMany(x => x.Credits)
			.Select(x => new { x.Amount, CreditDate = (DateTimeOffset?)x.CreditDate });
		// Use the aggregate only when no credit events exist, so partial credits never double count.
		// An unknown legacy date is included in All months, but never assigned to a specific month.
		var legacyCredits = query.SelectMany(x => x.Deposits)
			.Where(x => !x.Credits.Any() && x.CreditedAmount > 0m)
			.Select(x => new { Amount = x.CreditedAmount, x.CreditDate });
		var credits = await AsyncExecuter.ToListAsync(creditEvents.Concat(legacyCredits));
		return new CashActivitySummary
		{
			Collected = collections.Where(x => InPeriod(x.CollectedAt)).Sum(x => x.Amount),
			Spent = expenses.Where(x => InPeriod(x.PaymentDate)).Sum(x => x.Amount),
			Deposited = credits.Where(x => !input.Month.HasValue || x.CreditDate.HasValue && InPeriod(x.CreditDate.Value))
				.Sum(x => x.Amount)
		};
	}
}
