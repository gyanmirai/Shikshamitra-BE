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

	private static readonly IReadOnlyDictionary<string, LambdaExpression> SqlFilterFields = new Dictionary<string, LambdaExpression>
	{
		["name"] = (Expression<Func<ChargeCategory, string>>)(x => x.Name),
		["description"] = (Expression<Func<ChargeCategory, string?>>)(x => x.Description == null || x.Description == "" ? "-" : x.Description),
		["isApprovalRequired"] = (Expression<Func<ChargeCategory, int>>)(x => x.IsApprovalRequired ? 1 : 0),
		["isActive"] = (Expression<Func<ChargeCategory, int>>)(x => x.IsActive ? 1 : 0)
	};
	private sealed record IncomeFilterRow(Guid Id, string Name);
	private static readonly IReadOnlyDictionary<string, LambdaExpression> IncomeFilterFields = new Dictionary<string, LambdaExpression>
	{
		["incomeCategoryName"] = (Expression<Func<IncomeFilterRow, string>>)(x => x.Name)
	};
	private async Task<IQueryable<ChargeCategory>> ApplyListFiltersAsync(IQueryable<ChargeCategory> query, FinanceTableQuery filters)
	{
		query = filters.Apply(query, SqlFilterFields, column => column != "incomeCategoryName");
		if (!filters.Filters.Any(x => x.Column == "incomeCategoryName")) return query;
		// Accounting links remain canonical ABP ExtraProperties. Bound scalar reads to
		// filtered candidates and resolve labels per batch rather than hydrating all entities.
		var matchingIds = new List<Guid>();
		const int batchSize = 256;
		for (var offset = 0; ; offset += batchSize)
		{
			var batch = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Id).Skip(offset).Take(batchSize)
				.Select(x => new { x.Id, x.ExtraProperties }));
			var rows = batch.Select(x => new { x.Id, IncomeId = Guid.TryParse(x.ExtraProperties.GetValueOrDefault("ChargeIncomeCategoryId")?.ToString(), out var id) ? (Guid?)id : null }).ToList();
			var incomeIds = rows.Where(x => x.IncomeId.HasValue).Select(x => x.IncomeId!.Value).Distinct().ToArray();
			var names = await AsyncExecuter.ToListAsync((await transactionCategoryRepository.GetQueryableAsync())
				.Where(x => Enumerable.Contains(incomeIds, x.Id)).Select(x => new { x.Id, x.Name }));
			var byId = names.ToDictionary(x => x.Id, x => x.Name);
			var displayed = rows.Select(x => new IncomeFilterRow(x.Id, !x.IncomeId.HasValue ? "Income Category required" : byId.GetValueOrDefault(x.IncomeId.Value, "-")));
			matchingIds.AddRange(filters.Apply(displayed.AsQueryable(), IncomeFilterFields, column => column == "incomeCategoryName").Select(x => x.Id));
			if (batch.Count < batchSize) break;
		}
		var ids = matchingIds.ToArray();
		return query.Where(x => Enumerable.Contains(ids, x.Id));
	}
}
