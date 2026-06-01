using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Reports;

public class HrReportRequestDto
{
	[Required]
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

public class HrReportFilterDto
{
	public List<Guid>? EmployeeIds { get; set; }
	public List<string>? Departments { get; set; }
	public List<int>? Statuses { get; set; }
	public int? GenderType { get; set; }
	public string? Month { get; set; }
	public List<string>? ComponentNames { get; set; }
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

public class HrReportExportCreateResponseDto
{
	public Guid JobId { get; set; }
	public string Status { get; set; } = "Queued";
}

public class HrReportExportJobDto
{
	public Guid Id { get; set; }
	public string ReportType { get; set; } = string.Empty;
	public string Format { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public string? FileName { get; set; }
	public string? ErrorMessage { get; set; }
	public DateTime CreationTime { get; set; }
	public DateTime? StartedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
}

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

public class HrReportTemplateDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string DataSource { get; set; } = string.Empty;
	public string Visibility { get; set; } = HrReportTemplateVisibility.Private;
	public string Status { get; set; } = HrReportTemplateStatus.Draft;
	public Guid OwnerUserId { get; set; }
	public HrReportRequestDto Definition { get; set; } = new();
	public DateTime CreationTime { get; set; }
	public DateTime? LastModificationTime { get; set; }
}

public class HrReportTemplateListDto
{
	public List<HrReportTemplateDto> Items { get; set; } = new();
}

public class HrReportTemplateRequestDto
{
	[Required]
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	[Required]
	public string Category { get; set; } = string.Empty;
	[Required]
	public string DataSource { get; set; } = string.Empty;
	public string Visibility { get; set; } = HrReportTemplateVisibility.Private;
	public string Status { get; set; } = HrReportTemplateStatus.Draft;
	[Required]
	public HrReportRequestDto Definition { get; set; } = new();
}

public static class HrReportFormat
{
	public const string Pdf = "pdf";
	public const string Excel = "excel";
}

public static class HrReportFilterOperator
{
	public const string Equals = "equals";
	public const string Contains = "contains";
	public const string In = "in";
	public const string GreaterThanOrEqual = "gte";
	public const string LessThanOrEqual = "lte";
}

public static class HrReportSummaryFunction
{
	public const string Count = "count";
	public const string Sum = "sum";
	public const string Average = "avg";
}

public static class HrReportTemplateVisibility
{
	public const string Private = "private";
	public const string Organization = "organization";
}

public static class HrReportTemplateStatus
{
	public const string Draft = "Draft";
	public const string Published = "Published";
	public const string Archived = "Archived";
}

public static class HrReportDataSources
{
	public const string Employee = "employee";
	public const string Payroll = "payroll";
	public const string Attendance = "attendance";
	public const string Leave = "leave";
	public const string Policies = "policies";
	public const string Performance = "performance";
}

public static class HrReportTypes
{
	public const string EmployeeProfile = "Employee Profile";
	public const string EmployeeList = "Employee List";
	public const string JoiningLetter = "Joining Letter";
	public const string EmployeeDetail = "Employee Detail";
	public const string EmployeeContactList = "Employee Contact List";
	public const string EmployeeAllDetail = "Employee All Detail";
	public const string RelievingCertificate = "Relieving Certificate";
	public const string IncrementCertificate = "Increment Certificate";
	public const string ExperienceCertificate = "Experience Certificate";
	public const string SalaryRegister = "Salary Register";
	public const string EmployeePayslip = "Employee Payslip";
	public const string MonthlyPayrollSummary = "Monthly Payroll Summary";
	public const string AllowanceReport = "Allowance Report";
	public const string DeductionReport = "Deduction Report";
	public const string PayrollReconciliationReport = "Payroll Reconciliation Report";
	public const string LeaveEncashmentReport = "Leave Encashment Report";
	public const string FinalSettlementReport = "Final Settlement Report";
}

public interface IHrReportAppService : IApplicationService
{
	Task<HrReportSourcesDto> GetSourcesAsync();
	Task<HrReportOptionsDto> GetOptionsAsync();
	Task<HrReportTemplateListDto> GetTemplatesAsync();
	Task<HrReportTemplateDto> CreateTemplateAsync(HrReportTemplateRequestDto input);
	Task<HrReportTemplateDto> UpdateTemplateAsync(Guid id, HrReportTemplateRequestDto input);
	Task<HrReportTemplateDto> DuplicateTemplateAsync(Guid id);
	Task DeleteTemplateAsync(Guid id);
	Task<HrReportPreviewDto> PreviewAsync(HrReportRequestDto input);
	Task<HrReportExportCreateResponseDto> CreateExportAsync(HrReportRequestDto input);
	Task<List<HrReportExportJobDto>> GetExportsAsync();
	Task<HrReportExportJobDto> GetExportAsync(Guid jobId);
	Task<HrReportDownloadDto> DownloadExportAsync(Guid jobId);
}

public class HrReportDownloadDto
{
	public byte[] Bytes { get; set; } = Array.Empty<byte>();
	public string ContentType { get; set; } = string.Empty;
	public string FileName { get; set; } = string.Empty;
}
