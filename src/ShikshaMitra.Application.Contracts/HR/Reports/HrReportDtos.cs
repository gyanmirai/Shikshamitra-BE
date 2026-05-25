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

	public string Format { get; set; } = HrReportFormat.Excel;
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 50;
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
}

public class HrReportPreviewDto
{
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

public class HrReportOptionDto
{
	public string Id { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
}

public static class HrReportFormat
{
	public const string Pdf = "pdf";
	public const string Excel = "excel";
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
	Task<HrReportOptionsDto> GetOptionsAsync();
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
