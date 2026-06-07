using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.HR.Reports;

public static class HrReportRequestProcessor
{
	public const int MaxPreviewPageSize = 200;
	public const int MaxExportRows = 20000;

	public static void Normalize(HrReportRequestDto input, bool exportAllRows)
	{
		input.ReportType = (input.ReportType ?? string.Empty).Trim();
		input.DataSource = string.IsNullOrWhiteSpace(input.DataSource) ? null : input.DataSource.Trim().ToLowerInvariant();
		input.Format = string.Equals(input.Format, HrReportFormat.Pdf, StringComparison.OrdinalIgnoreCase) ? HrReportFormat.Pdf : HrReportFormat.Excel;
		input.Page = input.Page <= 0 ? 1 : input.Page;
		input.PageSize = exportAllRows ? MaxExportRows : Math.Clamp(input.PageSize <= 0 ? 50 : input.PageSize, 1, MaxPreviewPageSize);
		input.Filters ??= new HrReportFilterDto();
		input.Filters.Departments = input.Filters.Departments?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
		input.Filters.ComponentNames = input.Filters.ComponentNames?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
		input.Filters.DateField = string.IsNullOrWhiteSpace(input.Filters.DateField) ? null : input.Filters.DateField.Trim();
		input.Columns = input.Columns?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
		input.Groups = input.Groups?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
	}

	public static List<Dictionary<string, string?>> ShapeRows(
		List<Dictionary<string, string?>> rows,
		HrReportRequestDto input)
	{
		rows = ApplyConditionFilters(rows, input.Filters.Conditions);
		rows = ApplyGrouping(rows, input.Groups, input.Summaries);
		return ApplySort(rows, input.Sorts);
	}

	private static List<Dictionary<string, string?>> ApplyConditionFilters(List<Dictionary<string, string?>> rows, List<HrReportFilterConditionDto>? conditions)
	{
		if (conditions == null || conditions.Count == 0)
		{
			return rows;
		}

		return rows.Where(row => conditions.All(condition => Matches(row, condition))).ToList();
	}

	private static bool Matches(Dictionary<string, string?> row, HrReportFilterConditionDto condition)
	{
		if (string.IsNullOrWhiteSpace(condition.Field) || !row.TryGetValue(condition.Field, out var raw))
		{
			return true;
		}

		var value = raw ?? string.Empty;
		var expected = condition.Value ?? string.Empty;
		return condition.Operator switch
		{
			HrReportFilterOperator.Contains => value.Contains(expected, StringComparison.OrdinalIgnoreCase),
			HrReportFilterOperator.In => (condition.Values ?? []).Contains(value, StringComparer.OrdinalIgnoreCase),
			HrReportFilterOperator.GreaterThanOrEqual => Compare(value, expected) >= 0,
			HrReportFilterOperator.LessThanOrEqual => Compare(value, expected) <= 0,
			_ => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase)
		};
	}

	private static int Compare(string left, string right)
	{
		if (decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out var lnum)
			&& decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out var rnum))
		{
			return lnum.CompareTo(rnum);
		}

		if (DateTime.TryParse(left, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ldate)
			&& DateTime.TryParse(right, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var rdate))
		{
			return ldate.CompareTo(rdate);
		}

		return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
	}

	private static List<Dictionary<string, string?>> ApplyGrouping(List<Dictionary<string, string?>> rows, List<string>? groups, List<HrReportSummaryDto>? summaries)
	{
		groups = groups?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
		if (groups == null || groups.Count == 0)
		{
			return rows;
		}

		var summaryList = summaries?.Where(x => !string.IsNullOrWhiteSpace(x.Field)).ToList() ?? [];
		return rows
			.GroupBy(row => string.Join("\u001f", groups.Select(group => row.GetValueOrDefault(group) ?? string.Empty)))
			.Select(group =>
			{
				var first = group.First();
				var result = groups.ToDictionary(name => name, name => first.GetValueOrDefault(name));
				result["rowCount"] = group.Count().ToString(CultureInfo.InvariantCulture);
				foreach (var summary in summaryList)
				{
					var key = $"{summary.Function}_{summary.Field}";
					var values = group.Select(row => row.GetValueOrDefault(summary.Field)).Select(ParseDecimal).Where(x => x.HasValue).Select(x => x!.Value).ToList();
					result[key] = summary.Function switch
					{
						HrReportSummaryFunction.Sum => values.Sum().ToString("0.##", CultureInfo.InvariantCulture),
						HrReportSummaryFunction.Average => values.Count == 0 ? "0" : values.Average().ToString("0.##", CultureInfo.InvariantCulture),
						_ => group.Count().ToString(CultureInfo.InvariantCulture)
					};
				}

				return result;
			})
			.ToList();
	}

	private static List<Dictionary<string, string?>> ApplySort(List<Dictionary<string, string?>> rows, List<HrReportSortDto>? sorts)
	{
		sorts = sorts?.Where(x => !string.IsNullOrWhiteSpace(x.Field)).ToList();
		if (sorts == null || sorts.Count == 0)
		{
			return rows;
		}

		IOrderedEnumerable<Dictionary<string, string?>>? ordered = null;
		foreach (var sort in sorts)
		{
			Func<Dictionary<string, string?>, IComparable> key = row => SortValue(row.GetValueOrDefault(sort.Field));
			var desc = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
			ordered = ordered == null
				? (desc ? rows.OrderByDescending(key) : rows.OrderBy(key))
				: (desc ? ordered.ThenByDescending(key) : ordered.ThenBy(key));
		}

		return ordered?.ToList() ?? rows;
	}

	private static decimal? ParseDecimal(string? value) =>
		decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

	private static IComparable SortValue(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return (0, 0m, DateTime.MinValue, string.Empty);
		}

		if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
		{
			return (1, number, DateTime.MinValue, string.Empty);
		}

		if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
		{
			return (2, 0m, date, string.Empty);
		}

		return (3, 0m, DateTime.MinValue, value);
	}
}
