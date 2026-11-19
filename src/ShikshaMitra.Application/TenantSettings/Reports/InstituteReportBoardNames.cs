using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.Linq;

namespace ShikshaMitra.TenantSettings.Reports;

internal static class InstituteReportBoardNames
{
	public static async Task<Dictionary<Guid, string>> CustomAsync(IQueryable<EducationBoard> query, IEnumerable<Guid?> selected, IAsyncQueryableExecuter executor)
	{
		var ids = selected.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
		if (ids.Length == 0) return [];
		var rows = await executor.ToListAsync(query.Where(x => Enumerable.Contains(ids, x.Id)).Select(x => new { x.Id, x.ExtraProperties }));
		var names = new Dictionary<Guid, string>();
		foreach (var row in rows)
			if (row.ExtraProperties.TryGetValue("CustomName", out var value) && value != null) names[row.Id] = value.ToString()!;
		return names;
	}
}
