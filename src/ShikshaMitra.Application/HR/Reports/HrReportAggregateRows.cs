using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportAggregateRows
{
	public static void AddSummaryValues(
		Dictionary<string, string?> row,
		List<HrReportSummaryDto>? summaries,
		Dictionary<string, string?> valuesBySummaryKey)
	{
		foreach (var summary in summaries ?? [])
		{
			var key = $"{summary.Function}_{summary.Field}";
			if (valuesBySummaryKey.TryGetValue(key, out var value))
			{
				row[key] = value;
			}
		}
	}

	public static HrReportRowsResult BuildResult(
		List<Dictionary<string, string?>> rows,
		HrReportRequestDto input,
		bool exportAllRows,
		int totalCount,
		string groupField)
	{
		var sortedRows = SortRows(rows, input.Sorts, groupField);
		var skip = exportAllRows ? 0 : (input.Page - 1) * input.PageSize;
		var take = exportAllRows ? HrReportRequestProcessor.MaxExportRows : input.PageSize;
		return new HrReportRowsResult(sortedRows.Skip(skip).Take(take).ToList(), totalCount, true);
	}

	private static List<Dictionary<string, string?>> SortRows(
		List<Dictionary<string, string?>> rows,
		List<HrReportSortDto>? sorts,
		string groupField)
	{
		var firstSort = sorts?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Field));
		if (firstSort == null)
		{
			return rows
				.OrderByDescending(row => NumericSortValue(row, "rowCount"))
				.ThenBy(row => TextSortValue(row, groupField))
				.ToList();
		}

		var desc = string.Equals(firstSort.Direction, "desc", System.StringComparison.OrdinalIgnoreCase);
		return desc
			? rows.OrderByDescending(row => NumericSortValue(row, firstSort.Field)).ThenBy(row => TextSortValue(row, groupField)).ToList()
			: rows.OrderBy(row => NumericSortValue(row, firstSort.Field)).ThenBy(row => TextSortValue(row, groupField)).ToList();
	}

	private static decimal NumericSortValue(Dictionary<string, string?> row, string field) =>
		row.TryGetValue(field, out var value) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: 0;

	private static string TextSortValue(Dictionary<string, string?> row, string field) =>
		row.TryGetValue(field, out var value) ? value ?? string.Empty : string.Empty;
}
