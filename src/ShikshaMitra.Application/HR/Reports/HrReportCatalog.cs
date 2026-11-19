using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Reports;

internal static class HrReportCatalog
{
	public static List<HrReportOperatorDto> BuildOperators() =>
	[
		new() { Id = HrReportFilterOperator.Equals, Label = "Equals", Types = ["string", "number", "date", "boolean"] },
		new() { Id = HrReportFilterOperator.Contains, Label = "Contains", Types = ["string"] },
		new() { Id = HrReportFilterOperator.In, Label = "Is one of", Types = ["string", "number", "boolean"] },
		new() { Id = HrReportFilterOperator.GreaterThanOrEqual, Label = "On or after / >=", Types = ["date", "number"] },
		new() { Id = HrReportFilterOperator.LessThanOrEqual, Label = "On or before / <=", Types = ["date", "number"] }
	];

	public static List<HrReportOptionDto> BuildFormats() =>
	[
		new() { Id = HrReportFormat.Excel, Label = "Excel" },
		new() { Id = HrReportFormat.Pdf, Label = "PDF" }
	];

	public static HrReportSourceDto GetSource(string source)
	{
		return BuildSources().FirstOrDefault(x => x.Id == source.ToLowerInvariant())
			?? BuildSources().First(x => x.Id == HrReportDataSources.Employee);
	}

	public static List<HrReportColumnDto> ResolveColumns(string source, HrReportRequestDto input, List<Dictionary<string, string?>> rows)
	{
		var metadata = GetSource(source);
		var requested = input.Groups?.Count > 0
			? input.Groups.Concat(input.Summaries?.Select(x => $"{x.Function}_{x.Field}") ?? []).Prepend("rowCount").Distinct().ToList()
			: input.Columns is { Count: > 0 } ? input.Columns : metadata.DefaultColumns;

		if (source is HrReportDataSources.PayrollStatement or HrReportDataSources.PayrollRegister)
		{
			requested = requested.Concat(new[] { "month", "employeeCode", "employeeName", "totalEarnings", "totalDeduction", "netPay" }).Distinct().ToList();
			foreach (var deduction in new[] { "pf", "pt", "tds", "esic", "otherDeductions" })
					if (rows.Any(x => decimal.TryParse(x.GetValueOrDefault(deduction), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value) && value != 0m) && !requested.Contains(deduction)) requested.Add(deduction);
		}
		var fields = metadata.Fields.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
		return requested
			.Where(id => rows.Count == 0 || rows[0].ContainsKey(id) || fields.ContainsKey(id) || id == "rowCount" || id.Contains('_'))
			.Select(id => new HrReportColumnDto
			{
				Id = id,
				Label = fields.TryGetValue(id, out var field)
					? field.Label
					: id == "rowCount" ? "Rows" : Humanize(id)
			})
			.ToList();
	}

	public static bool IsGroupable(string source, string? field) =>
		FieldSupports(source, field, x => x.Groupable);

	public static bool IsSummarizable(string source, string? field) =>
		FieldSupports(source, field, x => x.Summarizable);

	public static bool IsChartable(string source, string? field) =>
		FieldSupports(source, field, x => x.Chartable);

	public static List<string> BuildCapabilityWarnings(string source, HrReportRequestDto input)
	{
		var warnings = new List<string>();
		foreach (var group in input.Groups ?? [])
		{
			if (!IsGroupable(source, group))
			{
				warnings.Add($"Field '{group}' is not supported for grouping in {GetSource(source).Label}.");
			}
		}

		foreach (var summary in input.Summaries ?? [])
		{
			if (!IsSummarizable(source, summary.Field))
			{
				warnings.Add($"Field '{summary.Field}' is not supported for summaries in {GetSource(source).Label}.");
			}
		}

		var chart = input.Chart;
		if (chart != null && !string.IsNullOrWhiteSpace(chart.Type) && chart.Type != HrReportChartType.None)
		{
			var categoryField = string.IsNullOrWhiteSpace(chart.CategoryField) ? input.Groups?.FirstOrDefault() : chart.CategoryField;
			if (!string.IsNullOrWhiteSpace(categoryField) && !IsChartable(source, categoryField))
			{
				warnings.Add($"Field '{categoryField}' is not supported for charts in {GetSource(source).Label}.");
			}

			if (!string.IsNullOrWhiteSpace(chart.ValueField) && chart.Function != HrReportSummaryFunction.Count && !IsChartable(source, chart.ValueField))
			{
				warnings.Add($"Field '{chart.ValueField}' is not supported for chart values in {GetSource(source).Label}.");
			}
		}

		return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
	}

	public static List<HrReportSourceDto> BuildSources()
		=> HrReportSourceDefinitions.Build();

	private static bool FieldSupports(string source, string? field, Func<HrReportFieldDto, bool> predicate)
	{
		if (string.IsNullOrWhiteSpace(field))
		{
			return false;
		}

		var metadata = GetSource(source);
		var sourceField = metadata.Fields.FirstOrDefault(x => string.Equals(x.Id, field, StringComparison.OrdinalIgnoreCase));
		return sourceField != null && predicate(sourceField);
	}

	private static string Humanize(string id) => string.Concat(id.Select((ch, index) => index > 0 && char.IsUpper(ch) ? $" {ch}" : ch.ToString()));
}
