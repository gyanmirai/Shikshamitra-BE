using System;
using System.Collections.Generic;
using FluentValidation;

namespace ShikshaMitra.HR.Reports;

public class HrReportRequestDto
{
	public string ReportType { get; set; } = string.Empty;

	public string? DataSource { get; set; }
	public string Format { get; set; } = HrReportFormat.Excel;
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 50;
	public List<string>? Columns { get; set; }
	public List<HrReportSortDto>? Sorts { get; set; }
	public List<string>? Groups { get; set; }
	public List<HrReportSummaryDto>? Summaries { get; set; }
	public HrReportFilterDto Filters { get; set; } = new();
}

public class HrReportRequestDtoValidator : AbstractValidator<HrReportRequestDto>
{
	public HrReportRequestDtoValidator()
	{
		RuleFor(x => x.ReportType).NotEmpty();
	}
}

public class HrReportFilterDto
{
	public List<Guid>? EmployeeIds { get; set; }
	public List<string>? Departments { get; set; }
	public List<int>? Statuses { get; set; }
	public int? GenderType { get; set; }
	public string? Month { get; set; }
	public List<string>? ComponentNames { get; set; }
	public string? DateField { get; set; }
	public DateOnly? FromDate { get; set; }
	public DateOnly? ToDate { get; set; }
	public List<HrReportFilterConditionDto>? Conditions { get; set; }
}

public class HrReportFilterConditionDto
{
	public string Field { get; set; } = string.Empty;
	public string Operator { get; set; } = HrReportFilterOperator.Equals;
	public string? Value { get; set; }
	public List<string>? Values { get; set; }
}

public class HrReportSortDto
{
	public string Field { get; set; } = string.Empty;
	public string Direction { get; set; } = "asc";
}

public class HrReportSummaryDto
{
	public string Field { get; set; } = string.Empty;
	public string Function { get; set; } = HrReportSummaryFunction.Count;
}
