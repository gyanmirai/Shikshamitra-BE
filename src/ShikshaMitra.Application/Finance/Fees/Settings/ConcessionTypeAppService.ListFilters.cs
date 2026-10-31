using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;

public partial class ConcessionTypeAppService
{
	private static readonly SettingsTableFilter<ConcessionType> ListFilter = new(
		new Dictionary<string, LambdaExpression>
		{
			["name"] = (Expression<Func<ConcessionType, string>>)(x => x.Name),
			["description"] = (Expression<Func<ConcessionType, string?>>)(x => x.Description == null || x.Description == "" ? "-" : x.Description),
			["documents"] = (Expression<Func<ConcessionType, string>>)(x => DisplayDocumentNames(x)),
			["deactivationReason"] = (Expression<Func<ConcessionType, string?>>)(x => x.DeactivationReason == null || x.DeactivationReason == "" ? "-" : x.DeactivationReason),
			["isActive"] = (Expression<Func<ConcessionType, int>>)(x => x.IsActive ? 1 : 0)
		});

	private static string DisplayDocumentNames(ConcessionType concession)
	{
		var names = string.Join(", ", concession.ConcessionTypeDocuments.Select(document => document.DocumentName));
		return string.IsNullOrEmpty(names) ? "-" : names;
	}

	private async Task<IQueryable<ConcessionType>> ApplyListFiltersAsync(IQueryable<ConcessionType> query, FinanceTableQuery filters)
	{
		if (filters.Filters.Count == 0) return query;
		// Evaluate display-only fields across the complete authorized candidate set before paging.
		var candidates = await AsyncExecuter.ToListAsync(query);
		var ids = ListFilter.Apply(candidates.AsQueryable(), filters).Select(x => x.Id).ToArray();
		return query.Where(x => Enumerable.Contains(ids, x.Id));
	}
}
