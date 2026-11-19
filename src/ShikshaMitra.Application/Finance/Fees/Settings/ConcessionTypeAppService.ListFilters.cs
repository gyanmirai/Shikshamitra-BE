using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;

public partial class ConcessionTypeAppService
{
	private static readonly IReadOnlyDictionary<string, LambdaExpression> SqlFilterFields = new Dictionary<string, LambdaExpression>
		{
			["name"] = (Expression<Func<ConcessionType, string>>)(x => x.Name),
			["description"] = (Expression<Func<ConcessionType, string?>>)(x => x.Description == null || x.Description == "" ? "-" : x.Description),
			["documents"] = (Expression<Func<ConcessionType, string>>)(x => DisplayDocumentNames(x)),
			["deactivationReason"] = (Expression<Func<ConcessionType, string?>>)(x => x.DeactivationReason == null || x.DeactivationReason == "" ? "-" : x.DeactivationReason),
			["isActive"] = (Expression<Func<ConcessionType, int>>)(x => x.IsActive ? 1 : 0)
		};
	private static readonly SettingsTableFilter<ConcessionType> ListFilter = new(SqlFilterFields);

	private static string DisplayDocumentNames(ConcessionType concession)
	{
		var names = string.Join(", ", concession.ConcessionTypeDocuments.Select(document => document.DocumentName));
		return string.IsNullOrEmpty(names) ? "-" : names;
	}

	private async Task<IQueryable<ConcessionType>> ApplyListFiltersAsync(IQueryable<ConcessionType> query, FinanceTableQuery filters)
	{
		query = filters.Apply(query, SqlFilterFields, column => column != "documents");
		if (!filters.Filters.Any(x => x.Column == "documents")) return query;
		// Joining document labels is display behavior. Read only the two required scalars
		// in bounded batches after all simple SQL predicates, never the audited entities.
		var matchingIds = new List<Guid>();
		const int batchSize = 256;
		for (var offset = 0; ; offset += batchSize)
		{
			var batch = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Id).Skip(offset).Take(batchSize)
				.Select(x => new { x.Id, Names = x.ConcessionTypeDocuments.Select(document => document.DocumentName).ToList() }));
			var rows = batch.Select(x => new DocumentFilterRow(x.Id, x.Names.Count == 0 ? "-" : string.Join(", ", x.Names)));
			matchingIds.AddRange(filters.Apply(rows.AsQueryable(), DocumentFilterFields, column => column == "documents").Select(x => x.Id));
			if (batch.Count < batchSize) break;
		}
		var ids = matchingIds.ToArray();
		return query.Where(x => Enumerable.Contains(ids, x.Id));
	}
	private sealed record DocumentFilterRow(Guid Id, string Names);
	private static readonly IReadOnlyDictionary<string, LambdaExpression> DocumentFilterFields = new Dictionary<string, LambdaExpression>
	{
		["documents"] = (Expression<Func<DocumentFilterRow, string>>)(x => x.Names)
	};
}
