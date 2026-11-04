using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeCategoryAppService
{
	private sealed record CategoryResponseContext(
		IReadOnlyDictionary<Guid, TransactionCategory> IncomeCategories,
		IReadOnlyDictionary<Guid, AccountHead> AccountHeads,
		IReadOnlyDictionary<Guid, FeeHeadDependenciesDto> Dependencies);

	private async Task<CategoryResponseContext> LoadResponseContextAsync(IReadOnlyCollection<FeeHead> heads)
	{
		if (heads.Count == 0) return new(new Dictionary<Guid, TransactionCategory>(),
			new Dictionary<Guid, AccountHead>(), new Dictionary<Guid, FeeHeadDependenciesDto>());
		var incomeIds = heads.Select(x => x.IncomeCategoryId).Distinct().ToArray();
		var incomeCategories = await incomeCategoryRepository.GetListAsync(x => Enumerable.Contains(incomeIds, x.Id));
		var accountIds = incomeCategories.Where(x => x.DefaultAccountHeadId.HasValue)
			.Select(x => x.DefaultAccountHeadId!.Value).Distinct().ToArray();
		var accounts = accountIds.Length == 0 ? new List<AccountHead>() :
			await accountHeadRepository.GetListAsync(x => Enumerable.Contains(accountIds, x.Id));
		var dependencies = await dependencyQuery.GetAsync(heads.Select(x => x.Id));
		return new(incomeCategories.ToDictionary(x => x.Id), accounts.ToDictionary(x => x.Id), dependencies);
	}

	private async Task<FeeCategoryResponse> MapToResponseAsync(FeeCategory category, List<FeeHead> heads)
		=> MapToResponse(category, heads, await LoadResponseContextAsync(heads));

	// Mapping performs no database reads: one context serves the entire requested page.
	private FeeCategoryResponse MapToResponse(FeeCategory category, IEnumerable<FeeHead> heads,
		CategoryResponseContext context)
	{
		var response = ObjectMapper.Map<FeeCategory, FeeCategoryResponse>(category);
		response.FeeHeads = heads.Select(head =>
		{
			var item = ObjectMapper.Map<FeeHead, FeeHeadResponse>(head);
			item.FeeCategoryName = category.Name;
			if (context.IncomeCategories.TryGetValue(head.IncomeCategoryId, out var income))
			{
				item.IncomeCategoryName = income.Name;
				if (income.DefaultAccountHeadId.HasValue &&
					context.AccountHeads.TryGetValue(income.DefaultAccountHeadId.Value, out var account))
				{
					item.IncomeAccountHeadId = account.Id;
					item.IncomeAccountHeadName = account.Name;
				}
			}
			item.Dependencies = context.Dependencies[head.Id];
			item.IsUsedInDownstream = item.Dependencies.IsBlocked;
			return item;
		}).ToList();
		response.IsUsedInDownstream = response.FeeHeads.Any(x => x.IsUsedInDownstream);
		return response;
	}
}
