using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Accounts_CashManagement;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

public partial class CashManagementAppService
{
	private async Task<CashActivitySummary> SummarizeActivityAsync(IQueryable<CashCollection> query,
		(DateTimeOffset Start, DateTimeOffset End)? period)
	{
		var facts = await reads.SummarizeActivityAsync(query, period?.Start, period?.End);
		return new CashActivitySummary { Collected = facts.Collected, Spent = facts.Spent, Deposited = facts.Deposited };
	}

	public async Task<CashExpensePage> GetExpensesAsync(CashQuery input)
	{
		await AuthorizationService.CheckAsync(P.View);
		ValidateReadInput(input);
		var (sources, _) = await ReadFilteredSourcesAsync(await ScopedQueryAsync(input.AcademicYearId), input);
		if (input.Status.HasValue) sources = sources.Where(x => x.Status == input.Status);
		var period = await ResolveActivityPeriodAsync(input);
		var (items, count) = await reads.ReadExpensesAsync(sources, period?.Start, period?.End,
			(input.Page - 1) * input.PageSize, input.PageSize);
		return new CashExpensePage(items.Select(item =>
			new CashExpenseRow(item.Id, item.Name, item.Amount, item.PaymentDate, item.SourceReference)).ToList(), count);
	}
}
