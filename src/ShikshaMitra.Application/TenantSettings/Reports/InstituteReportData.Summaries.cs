using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.TenantSettings.Reports;

public partial class InstituteReportData
{
	private async Task<(List<Dictionary<string, string?>> Rows, int Count)?> SummaryPageAsync(InstituteReportRequest request, Guid user, bool all)
	{
		var hierarchy = await HierarchyQueryAsync(user, request.Filters);
		var groups = hierarchy.Where(x => x.StandardId.HasValue).GroupBy(x => new {
			x.AcademicYearId, x.EducationBoardId, x.EducationMediumId, x.EducationMediumStageId, x.StandardId,
			x.Year, x.Board, x.Medium, x.Stage, x.Standard
		}).Select(group => new InstituteHierarchyRow {
			AcademicYearId = group.Key.AcademicYearId, EducationBoardId = group.Key.EducationBoardId,
			EducationMediumId = group.Key.EducationMediumId, EducationMediumStageId = group.Key.EducationMediumStageId,
			StandardId = group.Key.StandardId, Year = group.Key.Year, Board = group.Key.Board, Medium = group.Key.Medium,
			Stage = group.Key.Stage, Standard = group.Key.Standard,
			Capacity = group.Where(x => x.DivisionId.HasValue).Select(x => x.DivisionId).Distinct().Count()
		});
		var page = await InstituteReportQuery.PageAsync(groups, request, all, AsyncExecuter,
			q => q.OrderBy(x => x.Year).ThenBy(x => x.Medium).ThenBy(x => x.Standard).ThenBy(x => x.StandardId),
			HierarchyText(), new() { ["divisionCount"] = x => x.Capacity });
		if (page == null) return null;
		await HydrateBoardNamesAsync(page.Value.Items);
		var ids = page.Value.Items.Select(x => x.StandardId).ToArray();
		var children = await AsyncExecuter.ToListAsync(hierarchy.Where(x => Enumerable.Contains(ids, x.StandardId) && x.DivisionId.HasValue)
			.Select(x => new { x.StandardId, x.DivisionId, x.Division }));
		var lookup = children.ToLookup(x => x.StandardId);
		return (page.Value.Items.Select(x => {
			var row = HierarchyValues(x); row.Remove("division");
			row["divisionCount"] = (x.Capacity ?? 0).ToString(CultureInfo.InvariantCulture);
			row["divisions"] = string.Join(", ", lookup[x.StandardId].DistinctBy(d => d.DivisionId).Select(d => d.Division).OrderBy(d => d));
			return row;
		}).ToList(), page.Value.Count);
	}
}
