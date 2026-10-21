using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.TenantSettings.Reports;

public static class InstituteClassDivisionSummary
{
	// The input has already passed tenant, academic filters and UserScope; never count hidden divisions.
	public static List<Dictionary<string, string?>> Create(IEnumerable<InstituteHierarchyRow> hierarchy) =>
		hierarchy.Where(x => x.StandardId.HasValue)
			.GroupBy(x => new { x.AcademicYearId, x.EducationBoardId, x.EducationMediumId, x.EducationMediumStageId, x.StandardId })
			.Select(group => {
				var standard = group.First();
				var divisions = group.Where(x => x.DivisionId.HasValue).DistinctBy(x => x.DivisionId).ToList();
				return new Dictionary<string, string?> {
					["year"] = standard.Year, ["board"] = standard.Board, ["medium"] = standard.Medium,
					["stage"] = standard.Stage, ["standard"] = standard.Standard,
					["divisionCount"] = divisions.Count.ToString(CultureInfo.InvariantCulture),
					["divisions"] = string.Join(", ", divisions.Select(x => x.Division).OrderBy(x => x))
				};
			}).ToList();
}
