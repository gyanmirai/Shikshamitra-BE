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
		var facts = await reads.SummarizeActivityAsync(query,
			input.Month.HasValue ? new DateTimeOffset(period.StartUtc) : null,
			input.Month.HasValue ? new DateTimeOffset(period.EndUtc) : null);
		return new CashActivitySummary { Collected = facts.Collected, Spent = facts.Spent, Deposited = facts.Deposited };
	}
}
