using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Reports;

public class HrReportRequestValidationService : ITransientDependency
{
	private static readonly HashSet<string> AllowedSortDirections = new(StringComparer.OrdinalIgnoreCase)
	{
		"asc",
		"desc"
	};

	private static readonly HashSet<string> AllowedSummaryFunctions = new(StringComparer.OrdinalIgnoreCase)
	{
		HrReportSummaryFunction.Count,
		HrReportSummaryFunction.Sum,
		HrReportSummaryFunction.Average
	};

	private static readonly HashSet<string> AllowedChartTypes = new(StringComparer.OrdinalIgnoreCase)
	{
		HrReportChartType.None,
		HrReportChartType.Kpi,
		HrReportChartType.Bar,
		HrReportChartType.Line,
		HrReportChartType.Donut
	};

	public void Validate(HrReportRequestDto input)
	{
		HrReportRequestProcessor.Normalize(input, exportAllRows: false);
		CanonicalizeEnumLikeValues(input);
		var source = ResolveSource(input);
		var metadata = HrReportCatalog.GetSource(source);
		var fields = metadata.Fields.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
		var generatedFields = BuildGeneratedFields(input);
		var errors = new List<string>();

		ValidateColumns(input.Columns, fields, generatedFields, errors);
		ValidateSorts(input.Sorts, fields, generatedFields, errors);
		ValidateGroups(input.Groups, fields, errors);
		ValidateSummaries(input.Summaries, fields, errors);
		ValidateFilters(input.Filters, fields, errors);
		ValidateChart(input, fields, errors);

		if (errors.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.ReportRequestInvalid)
				.WithData("dataSource", metadata.Id)
				.WithData("errors", string.Join("; ", errors.Distinct(StringComparer.OrdinalIgnoreCase)));
		}
	}

	private static void CanonicalizeEnumLikeValues(HrReportRequestDto input)
	{
		foreach (var sort in input.Sorts ?? [])
		{
			sort.Direction = CanonicalizeToken(sort.Direction);
		}

		foreach (var summary in input.Summaries ?? [])
		{
			summary.Function = CanonicalizeToken(summary.Function);
		}

		if (input.Chart != null)
		{
			input.Chart.Type = CanonicalizeToken(input.Chart.Type);
			if (!string.IsNullOrWhiteSpace(input.Chart.Function))
			{
				input.Chart.Function = CanonicalizeToken(input.Chart.Function);
			}
		}

		foreach (var condition in input.Filters.Conditions ?? [])
		{
			condition.Operator = CanonicalizeToken(condition.Operator);
		}
	}

	private static string CanonicalizeToken(string? value) =>
		string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

	private static void ValidateColumns(
		IEnumerable<string>? columns,
		IReadOnlyDictionary<string, HrReportFieldDto> fields,
		IReadOnlySet<string> generatedFields,
		List<string> errors)
	{
		foreach (var column in columns ?? [])
		{
			if (!fields.ContainsKey(column) && !generatedFields.Contains(column))
			{
				errors.Add($"Unknown column '{column}'.");
			}
		}
	}

	private static void ValidateSorts(
		IEnumerable<HrReportSortDto>? sorts,
		IReadOnlyDictionary<string, HrReportFieldDto> fields,
		IReadOnlySet<string> generatedFields,
		List<string> errors)
	{
		foreach (var sort in sorts ?? [])
		{
			if (!AllowedSortDirections.Contains(sort.Direction))
			{
				errors.Add($"Sort direction '{sort.Direction}' is not valid for field '{sort.Field}'.");
			}

			if (generatedFields.Contains(sort.Field))
			{
				continue;
			}

			if (!TryGetField(fields, sort.Field, out var field))
			{
				errors.Add($"Unknown sort field '{sort.Field}'.");
			}
			else if (!field.Sortable)
			{
				errors.Add($"Field '{sort.Field}' cannot be sorted.");
			}
		}
	}

	private static IReadOnlySet<string> BuildGeneratedFields(HrReportRequestDto input)
	{
		var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (input.Groups is { Count: > 0 })
		{
			fields.Add("rowCount");
		}

		foreach (var summary in input.Summaries ?? [])
		{
			if (!string.IsNullOrWhiteSpace(summary.Field))
			{
				fields.Add($"{summary.Function}_{summary.Field}");
			}
		}

		return fields;
	}

	private static void ValidateGroups(
		IEnumerable<string>? groups,
		IReadOnlyDictionary<string, HrReportFieldDto> fields,
		List<string> errors)
	{
		foreach (var group in groups ?? [])
		{
			if (!TryGetField(fields, group, out var field))
			{
				errors.Add($"Unknown group field '{group}'.");
			}
			else if (!field.Groupable)
			{
				errors.Add($"Field '{group}' cannot be grouped.");
			}
		}
	}

	private static void ValidateSummaries(
		IEnumerable<HrReportSummaryDto>? summaries,
		IReadOnlyDictionary<string, HrReportFieldDto> fields,
		List<string> errors)
	{
		foreach (var summary in summaries ?? [])
		{
			if (!AllowedSummaryFunctions.Contains(summary.Function))
			{
				errors.Add($"Summary function '{summary.Function}' is not valid for field '{summary.Field}'.");
			}

			if (!TryGetField(fields, summary.Field, out var field))
			{
				errors.Add($"Unknown summary field '{summary.Field}'.");
			}
			else if (!field.Summarizable)
			{
				errors.Add($"Field '{summary.Field}' cannot be summarized.");
			}
		}
	}

	private static void ValidateFilters(
		HrReportFilterDto filters,
		IReadOnlyDictionary<string, HrReportFieldDto> fields,
		List<string> errors)
	{
		if (!string.IsNullOrWhiteSpace(filters.DateField))
		{
			if (!TryGetField(fields, filters.DateField, out var dateField))
			{
				errors.Add($"Unknown date field '{filters.DateField}'.");
			}
			else if (!string.Equals(dateField.Type, "date", StringComparison.OrdinalIgnoreCase))
			{
				errors.Add($"Field '{filters.DateField}' is not a date field.");
			}
		}

		foreach (var condition in filters.Conditions ?? [])
		{
			if (!TryGetField(fields, condition.Field, out var field))
			{
				errors.Add($"Unknown filter field '{condition.Field}'.");
			}
			else if (!field.Filterable)
			{
				errors.Add($"Field '{condition.Field}' cannot be filtered.");
			}
			else if (!field.Operators.Contains(condition.Operator, StringComparer.OrdinalIgnoreCase))
			{
				errors.Add($"Operator '{condition.Operator}' is not valid for field '{condition.Field}'.");
			}
		}
	}

	private static void ValidateChart(
		HrReportRequestDto input,
		IReadOnlyDictionary<string, HrReportFieldDto> fields,
		List<string> errors)
	{
		var chart = input.Chart;
		if (chart == null || string.IsNullOrWhiteSpace(chart.Type))
		{
			return;
		}

		if (!AllowedChartTypes.Contains(chart.Type))
		{
			errors.Add($"Chart type '{chart.Type}' is not valid.");
			return;
		}

		if (chart.Type == HrReportChartType.None)
		{
			return;
		}

		var chartFunction = string.IsNullOrWhiteSpace(chart.Function)
			? HrReportSummaryFunction.Count
			: chart.Function;
		if (!AllowedSummaryFunctions.Contains(chartFunction))
		{
			errors.Add($"Chart function '{chartFunction}' is not valid.");
		}

		var categoryField = string.IsNullOrWhiteSpace(chart.CategoryField)
			? input.Groups?.FirstOrDefault()
			: chart.CategoryField;
		if (!string.IsNullOrWhiteSpace(categoryField))
		{
			ValidateChartField(categoryField, fields, errors, "chart category");
		}

		if (!string.IsNullOrWhiteSpace(chart.ValueField))
		{
			ValidateChartField(chart.ValueField, fields, errors, "chart value");
		}
	}

	private static void ValidateChartField(
		string fieldId,
		IReadOnlyDictionary<string, HrReportFieldDto> fields,
		List<string> errors,
		string purpose)
	{
		if (!TryGetField(fields, fieldId, out var field))
		{
			errors.Add($"Unknown {purpose} field '{fieldId}'.");
		}
		else if (!field.Chartable)
		{
			errors.Add($"Field '{fieldId}' cannot be used as a {purpose}.");
		}
	}

	private static bool TryGetField(
		IReadOnlyDictionary<string, HrReportFieldDto> fields,
		string? fieldId,
		out HrReportFieldDto field)
	{
		if (!string.IsNullOrWhiteSpace(fieldId) && fields.TryGetValue(fieldId, out field!))
		{
			return true;
		}

		field = null!;
		return false;
	}

	private static string ResolveSource(HrReportRequestDto input)
	{
		var source = !string.IsNullOrWhiteSpace(input.DataSource)
			? input.DataSource
			: input.ReportType switch
			{
				HrReportTypes.SalaryRegister or HrReportTypes.EmployeePayslip or HrReportTypes.MonthlyPayrollSummary
					or HrReportTypes.AllowanceReport or HrReportTypes.DeductionReport or HrReportTypes.PayrollReconciliationReport
					or HrReportTypes.LeaveEncashmentReport or HrReportTypes.FinalSettlementReport => HrReportDataSources.Payroll,
				_ => HrReportDataSources.Employee
			};

		var knownSource = HrReportCatalog.BuildSources()
			.FirstOrDefault(item => string.Equals(item.Id, source, StringComparison.OrdinalIgnoreCase));
		if (knownSource == null)
		{
			throw new BusinessException(HRErrorCodes.ReportRequestInvalid)
				.WithData("dataSource", source)
				.WithData("errors", $"Unknown data source '{source}'.");
		}

		return knownSource.Id;
	}
}
