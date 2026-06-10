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
	public List<string> QueryWarnings { get; set; } = new();
	public HrReportChartPreviewDto? Chart { get; set; }
}

public class HrReportColumnDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
}

public class HrReportChartPreviewDto
{
	public string Type { get; set; } = HrReportChartType.None;
	public string Title { get; set; } = string.Empty;
	public List<HrReportChartPointDto> Points { get; set; } = new();
}

public class HrReportChartPointDto
{
	public string Label { get; set; } = string.Empty;
	public decimal Value { get; set; }
}
