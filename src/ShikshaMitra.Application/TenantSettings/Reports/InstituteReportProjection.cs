using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.TenantSettings.Reports;

public static class InstituteReportProjection
{
	public static IEnumerable<Dictionary<string, string?>> Apply(IEnumerable<Dictionary<string, string?>> rows, InstituteReportRequest request)
	{
		foreach (var condition in request.Conditions) rows = rows.Where(row => Matches(row.GetValueOrDefault(condition.Field), condition));
		if (request.SortField != null && InstituteReportCatalog.IsNumeric(request.SortField)) rows = request.Descending
			? rows.OrderByDescending(x => SortNumber(x.GetValueOrDefault(request.SortField)))
			: rows.OrderBy(x => SortNumber(x.GetValueOrDefault(request.SortField)));
		else if (request.SortField != null) rows = request.Descending
			? rows.OrderByDescending(x => x.GetValueOrDefault(request.SortField), StringComparer.OrdinalIgnoreCase)
			: rows.OrderBy(x => x.GetValueOrDefault(request.SortField), StringComparer.OrdinalIgnoreCase);
		return rows;
	}
	private static decimal SortNumber(string? value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : decimal.MinValue;
	private static bool Matches(string? value, InstituteReportCondition condition) => condition.Operator switch
	{
		"eq" => InstituteReportCatalog.IsNumeric(condition.Field) ? NumericCompare(value, condition.Value) is 0 : string.Equals(value ?? "", condition.Value, StringComparison.OrdinalIgnoreCase),
		"contains" => (value ?? "").Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
		"gt" => NumericCompare(value, condition.Value) is > 0,
		"lt" => NumericCompare(value, condition.Value) is < 0,
		_ => false
	};
	private static int? NumericCompare(string? left, string right) => decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) && decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out var b) ? a.CompareTo(b) : null;
}
