using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Domain.Finance.Fees.Settings;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class ChargeCategoryAppService
{
	private sealed record FilterRow(ChargeCategory Category, string IncomeCategoryName);
	private static readonly SettingsTableFilter<FilterRow> ListFilter = new(
		new Dictionary<string, LambdaExpression>
		{
			["name"] = (Expression<Func<FilterRow, string>>)(x => x.Category.Name),
			["incomeCategoryName"] = (Expression<Func<FilterRow, string>>)(x => x.IncomeCategoryName),
			["description"] = (Expression<Func<FilterRow, string?>>)(x => x.Category.Description == null || x.Category.Description == "" ? "-" : x.Category.Description),
			["isApprovalRequired"] = (Expression<Func<FilterRow, int>>)(x => x.Category.IsApprovalRequired ? 1 : 0),
			["isActive"] = (Expression<Func<FilterRow, int>>)(x => x.Category.IsActive ? 1 : 0)
		});

	private async Task<IQueryable<ChargeCategory>> ApplyListFiltersAsync(IQueryable<ChargeCategory> query, FinanceTableQuery filters)
	{
		if (filters.Filters.Count == 0) return query;
		// Accounting routing is stored in ExtraProperties, so resolve names before counting/paging.
		var candidates = await AsyncExecuter.ToListAsync(query);
		var incomeIds = candidates.Where(x => x.IncomeCategoryId.HasValue).Select(x => x.IncomeCategoryId!.Value).Distinct().ToArray();
		var names = await AsyncExecuter.ToListAsync((await transactionCategoryRepository.GetQueryableAsync())
			.Where(x => Enumerable.Contains(incomeIds, x.Id)).Select(x => new { x.Id, x.Name }));
		var byId = names.ToDictionary(x => x.Id, x => x.Name);
		var rows = candidates.Select(x => new FilterRow(x, !x.IsAccountingReady ? "Income Category required" :
			x.IncomeCategoryId.HasValue && byId.TryGetValue(x.IncomeCategoryId.Value, out var name) ? name : "-"));
		var ids = ListFilter.Apply(rows.AsQueryable(), filters).Select(x => x.Category.Id).ToArray();
		return query.Where(x => Enumerable.Contains(ids, x.Id));
	}
}
