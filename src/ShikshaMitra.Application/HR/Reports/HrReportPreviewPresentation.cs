using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportPreviewPresentation
{
	public static List<string> BuildWarnings(
		string source,
		HrReportRequestDto input,
		bool exportAllRows,
		int totalCount,
		bool isDatabaseBacked)
	{
		var warnings = HrReportCatalog.BuildCapabilityWarnings(source, input);
		if (source is HrReportDataSources.PayrollStatement or HrReportDataSources.PayrollRegister)
			warnings.Add("Issued payroll only. Unavailable months are not counted as zero pay.");
		if (exportAllRows && totalCount >= HrReportRequestProcessor.MaxExportRows)
		{
			warnings.Add($"Export capped at {HrReportRequestProcessor.MaxExportRows} rows. Narrow filters for a complete extract.");
		}

		if (!isDatabaseBacked && (input.Groups is { Count: > 0 } || input.Summaries is { Count: > 0 } || input.Filters.Conditions is { Count: > 0 }))
		{
			warnings.Add("Advanced grouping, summaries, and ad-hoc conditions are applied after source projection for this release.");
		}

		return warnings;
	}

	public static HrReportChartPreviewDto? BuildChartPreview(
		HrReportRequestDto input,
		List<Dictionary<string, string?>> rows)
	{
		var chart = input.Chart;
		if (chart == null || string.IsNullOrWhiteSpace(chart.Type) || chart.Type == HrReportChartType.None)
		{
			return null;
		}

		if (chart.Type == HrReportChartType.Kpi)
		{
			return new HrReportChartPreviewDto
			{
				Type = HrReportChartType.Kpi,
				Title = string.IsNullOrWhiteSpace(chart.Title) ? input.ReportType : chart.Title!,
				Points =
				[
					new HrReportChartPointDto
					{
						Label = "Rows",
						Value = rows.Count
					}
				]
			};
		}

		var categoryField = chart.CategoryField;
		if (string.IsNullOrWhiteSpace(categoryField))
		{
			categoryField = input.Groups?.FirstOrDefault();
		}

		if (string.IsNullOrWhiteSpace(categoryField))
		{
			return new HrReportChartPreviewDto
			{
				Type = chart.Type,
				Title = string.IsNullOrWhiteSpace(chart.Title) ? input.ReportType : chart.Title!,
				Points = []
			};
		}

		var valueField = chart.ValueField;
		var function = string.IsNullOrWhiteSpace(chart.Function) ? HrReportSummaryFunction.Count : chart.Function;
		var points = rows
			.GroupBy(row => row.GetValueOrDefault(categoryField) ?? "Unspecified")
			.Select(group => new HrReportChartPointDto
			{
				Label = group.Key,
				Value = Aggregate(group, valueField, function, input.Groups)
			})
			.OrderByDescending(point => point.Value)
			.ThenBy(point => point.Label)
			.Take(20)
			.ToList();

		return new HrReportChartPreviewDto
		{
			Type = chart.Type,
			Title = string.IsNullOrWhiteSpace(chart.Title) ? input.ReportType : chart.Title!,
			Points = points
		};
	}

	private static decimal Aggregate(
		IEnumerable<Dictionary<string, string?>> rows,
		string? valueField,
		string? function,
		List<string>? groups)
	{
		var rowList = rows.ToList();
		if (string.IsNullOrWhiteSpace(valueField) || function == HrReportSummaryFunction.Count)
		{
			if (groups is { Count: > 0 })
			{
				return rowList
					.Select(row => row.GetValueOrDefault("rowCount"))
					.Select(ParseDecimal)
					.Where(value => value.HasValue)
					.Select(value => value!.Value)
					.DefaultIfEmpty(rowList.Count)
					.Sum();
			}

			return rowList.Count;
		}

		if (groups is { Count: > 0 })
		{
			var summaryKey = $"{function}_{valueField}";
			var summarizedValues = rowList
				.Select(row => row.GetValueOrDefault(summaryKey))
				.Select(ParseDecimal)
				.Where(value => value.HasValue)
				.Select(value => value!.Value)
				.ToList();
			if (summarizedValues.Count > 0)
			{
				return summarizedValues.Sum();
			}
		}

		var values = rowList
			.Select(row => row.GetValueOrDefault(valueField))
			.Select(ParseDecimal)
			.Where(value => value.HasValue)
			.Select(value => value!.Value)
			.ToList();

		return function == HrReportSummaryFunction.Average
			? values.Count == 0 ? 0 : values.Average()
			: values.Sum();
	}

	private static decimal? ParseDecimal(string? value) =>
		decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
