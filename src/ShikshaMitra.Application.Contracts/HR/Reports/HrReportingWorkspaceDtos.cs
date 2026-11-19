using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Reports;

public class HrReportDesignDto
{
	public int Version { get; set; } = 2;
	public string DataSource { get; set; } = "employee";
	public string Layout { get; set; } = "detail";
	public List<HrReportDisplayColumnDto> Columns { get; set; } = [];
	public HrReportConditionGroupDto Filters { get; set; } = new();
	public List<HrReportSortDto> Sorts { get; set; } = [];
	public List<string> Groups { get; set; } = [];
	public List<HrReportSummaryDto> Summaries { get; set; } = [];
	public HrReportChartDefinitionDto? Chart { get; set; }
	public List<string> Parameters { get; set; } = ["period", "departments", "employeeIds"];
	public string DefaultPeriod { get; set; } = "thisMonth";
	public string DateField { get; set; } = "";
	public string PaperSize { get; set; } = "A4";
	public string Orientation { get; set; } = "landscape";
}

public class HrReportDisplayColumnDto
{
	public string Field { get; set; } = "";
	public string Label { get; set; } = "";
	public string Format { get; set; } = "auto";
}

public class HrReportConditionGroupDto
{
	public string Match { get; set; } = "all";
	public List<HrReportFilterConditionDto> Conditions { get; set; } = [];
	public List<HrReportConditionGroupDto> Groups { get; set; } = [];
}

public class HrReportParametersDto
{
	public Dictionary<string, HrReportFilterValueDto> FilterValues { get; set; } = [];
	public string TimeZone { get; set; } = "Asia/Kolkata";
	public string Period { get; set; } = "";
	public string? Month { get; set; }
	public DateOnly? FromDate { get; set; }
	public DateOnly? ToDate { get; set; }
	public List<Guid> EmployeeIds { get; set; } = [];
	public List<string> Departments { get; set; } = [];
	public Guid? AcademicYearId { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public Guid? CycleId { get; set; }
	public int? EmployeeGroup { get; set; }
	public bool IncludeDrafts { get; set; }
}

public class HrReportFilterValueDto
{
	public string? Value { get; set; }
	public List<string>? Values { get; set; }
}

public class HrReportLibraryTemplateDto
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public string Description { get; set; } = "";
	public string Category { get; set; } = "";
	public string Visibility { get; set; } = "private";
	public string Status { get; set; } = "Draft";
	public int Revision { get; set; }
	public int PublishedRevision { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public Guid? OwnerUserId { get; set; }
	public bool IsBuiltIn { get; set; }
	public bool CanEdit { get; set; }
	public HrReportDesignDto Definition { get; set; } = new();
}

public class HrReportSaveDesignDto
{
	public string Name { get; set; } = "";
	public string Description { get; set; } = "";
	public string Visibility { get; set; } = "private";
	public string? ConcurrencyStamp { get; set; }
	public bool Publish { get; set; }
	public HrReportDesignDto Definition { get; set; } = new();
}

public class HrReportCreateRunDto
{
	public string TemplateId { get; set; } = "";
	public int Revision { get; set; }
	public HrReportParametersDto Parameters { get; set; } = new();
}

public class HrReportDesignerPreviewDto
{
	public HrReportDesignDto Definition { get; set; } = new();
	public HrReportParametersDto Parameters { get; set; } = new();
}

public class HrReportDatasetDto
{
	public List<string> RequiredParameters { get; set; } = [];
	public string Id { get; set; } = "";
	public string Label { get; set; } = "";
	public string Category { get; set; } = "";
	public string RowMeaning { get; set; } = "";
	public List<HrReportFieldDto> Fields { get; set; } = [];
	public List<string> Layouts { get; set; } = ["detail", "summary"];
	public List<string> Parameters { get; set; } = ["period", "departments", "employeeIds"];
	public List<string> SummaryFunctions { get; set; } = ["count", "distinct", "sum", "avg"];
}

public class HrReportRunHistoryDto
{
	public List<HrReportRunDto> Items { get; set; } = [];
	public bool HasMore { get; set; }
}

public class HrReportRunDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = "";
	public string TemplateId { get; set; } = "";
	public int Revision { get; set; }
	public string DataSource { get; set; } = "";
	public string Status { get; set; } = "Queued";
	public DateTimeOffset CapturedAt { get; set; }
	public DateTimeOffset ExpiresAt { get; set; }
	public int RowCount { get; set; }
	public string? ErrorMessage { get; set; }
	public bool IsExpired { get; set; }
	public bool ExcelReady { get; set; }
	public bool PdfReady { get; set; }
	public string ExcelStatus { get; set; } = "NotRequested";
	public string PdfStatus { get; set; } = "NotRequested";
	public HrReportParametersDto Parameters { get; set; } = new();
	public List<HrReportFieldDto> Columns { get; set; } = [];
	public Dictionary<string, decimal?> Totals { get; set; } = [];
	public List<string> Warnings { get; set; } = [];
	public HrReportChartPreviewDto? Chart { get; set; }
}

public class HrReportResultPageDto
{
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public List<Dictionary<string, object?>> Rows { get; set; } = [];
}

public class HrReportWorkspaceOptionsDto
{
	public List<HrReportOptionDto> Staff { get; set; } = [];
	public List<HrReportOptionDto> StaffGroups { get; set; } = [];
	public List<HrReportOptionDto> Departments { get; set; } = [];
	public List<HrReportOptionDto> AcademicYears { get; set; } = [];
	public List<HrReportOptionDto> Sections { get; set; } = [];
	public List<HrReportOptionDto> Cycles { get; set; } = [];
}
