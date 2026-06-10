using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportPolicyRowSort
{
	public static IQueryable<T> Apply<T>(IQueryable<T> query, List<HrReportSortDto>? sorts)
	{
		var firstSort = sorts?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Field));
		if (firstSort == null)
		{
			return query.OrderBy("SortDate descending, RecordType ascending, Entity ascending");
		}

		var desc = string.Equals(firstSort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
		return firstSort.Field switch
		{
			"recordType" => OrderByProperty(query, "RecordType", desc),
			"entity" => OrderByProperty(query, "Entity", desc),
			"action" => OrderByProperty(query, "Action", desc),
			"target" => OrderByProperty(query, "Target", desc),
			"effectiveFrom" => OrderByProperty(query, "EffectiveFrom", desc),
			"effectiveTo" => OrderByProperty(query, "EffectiveTo", desc),
			"isActive" => OrderByProperty(query, "IsActive", desc),
			"occurredAt" => OrderByProperty(query, "OccurredAt", desc),
			_ => OrderByProperty(query, "SortDate", desc)
		};
	}

	private static IQueryable<T> OrderByProperty<T>(IQueryable<T> query, string propertyName, bool descending) =>
		query.OrderBy($"{propertyName} {(descending ? "descending" : "ascending")}");
}
