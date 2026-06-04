using System.Collections.Generic;

namespace ShikshaMitra.HR.Reports;

public class HrReportOptionsDto
{
	public List<HrReportOptionDto> ReportTypes { get; set; } = new();
	public List<HrReportOptionDto> Departments { get; set; } = new();
	public List<HrReportOptionDto> Statuses { get; set; } = new();
}

public class HrReportSourcesDto
{
	public List<HrReportSourceDto> Sources { get; set; } = new();
	public List<HrReportOperatorDto> Operators { get; set; } = new();
	public List<HrReportOptionDto> Formats { get; set; } = new();
}

public class HrReportSourceDto
{
	public string Id { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public List<HrReportFieldDto> Fields { get; set; } = new();
	public List<string> DefaultColumns { get; set; } = new();
	public List<string> DefaultSorts { get; set; } = new();
}

public class HrReportFieldDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public string Type { get; set; } = "string";
	public bool Filterable { get; set; } = true;
	public bool Sortable { get; set; } = true;
	public bool Groupable { get; set; }
	public bool Summarizable { get; set; }
	public List<string> Operators { get; set; } = new();
	public List<HrReportOptionDto> Options { get; set; } = new();
}

public class HrReportOperatorDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public List<string> Types { get; set; } = new();
}

public class HrReportOptionDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
}
