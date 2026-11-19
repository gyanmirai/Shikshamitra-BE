using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class ChargeCategoryAppService
{
	private sealed record AccountingLabels(Dictionary<Guid, string> Income, Dictionary<Guid, string> Accounts);

	private async Task<AccountingLabels> LoadAccountingLabelsAsync(IEnumerable<ChargeCategory> categories)
	{
		var rows = categories.ToList();
		var incomeIds = rows.Where(x => x.IncomeCategoryId.HasValue).Select(x => x.IncomeCategoryId!.Value).Distinct().ToArray();
		var accountIds = rows.Where(x => x.IncomeAccountHeadId.HasValue).Select(x => x.IncomeAccountHeadId!.Value).Distinct().ToArray();
		var income = incomeIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(
			(await transactionCategoryRepository.GetQueryableAsync()).Where(x => Enumerable.Contains(incomeIds, x.Id))
				.Select(x => new { x.Id, x.Name }));
		// Sequential batches share the request-scoped DbContext.
		var accounts = accountIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(
			(await accountHeadRepository.GetQueryableAsync()).Where(x => Enumerable.Contains(accountIds, x.Id))
				.Select(x => new { x.Id, x.Name }));
		return new AccountingLabels(income.ToDictionary(x => x.Id, x => x.Name), accounts.ToDictionary(x => x.Id, x => x.Name));
	}

	private static string? MappingName(Dictionary<Guid, string> labels, Guid? id) =>
		id.HasValue ? labels.GetValueOrDefault(id.Value) : null;

	private static ChargeCategoryResponse MapResponse(ChargeCategory category, AccountingLabels labels) => new()
	{
		Id = category.Id, Name = category.Name,
		IsApprovalRequired = category.IsApprovalRequired,
		Description = category.Description, IsActive = category.IsActive,
		IncomeCategoryId = category.IncomeCategoryId,
		IncomeAccountHeadId = category.IncomeAccountHeadId,
		IncomeCategoryName = MappingName(labels.Income, category.IncomeCategoryId),
		IncomeAccountHeadName = MappingName(labels.Accounts, category.IncomeAccountHeadId),
		IsAccountingReady = category.IsAccountingReady,
		ConcurrencyStamp = category.ConcurrencyStamp
	};
}
