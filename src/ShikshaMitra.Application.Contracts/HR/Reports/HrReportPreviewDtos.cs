using System.Collections.Generic;

namespace ShikshaMitra.HR.Reports;

public class HrReportPreviewDto
{
	public string DataSource { get; set; } = string.Empty;
	public string ReportType { get; set; } = string.Empty;
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public List<HrReportColumnDto> Columns { get; set; } = new();
	public List<Dictionary<string, string?>> Rows { get; set; } = new();
	public HrReportFilterDto NormalizedFilters { get; set; } = new();
}

public class HrReportColumnDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
}
