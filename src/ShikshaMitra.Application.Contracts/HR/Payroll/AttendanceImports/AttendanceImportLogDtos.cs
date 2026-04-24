using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public enum AttendanceImportLogStatus
{
	Preview = 1,
	Confirmed = 2,
	BatchLocked = 3,
	PayrollLocked = 4
}

public class GetAttendanceImportLogListInput : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public string? PayrollMonth { get; set; }
}

public class GetAttendanceImportRowsInput : PagedAndSortedResultRequestDto
{
}

public enum AttendanceImportRowStatusCode
{
	Present = 0,
	Absent = 1,
	HalfDay = 2
}

public class AttendanceImportLogListItemDto
{
	public Guid Id { get; set; }
	public string FileName { get; set; } = string.Empty;
	public Guid ImportedByUserId { get; set; }
	public string ImportedByName { get; set; } = string.Empty;
	public DateTime ImportedAt { get; set; }
	public AttendanceImportLogStatus Status { get; set; } = AttendanceImportLogStatus.Preview;
	public string? PayrollMonth { get; set; }
	public int TotalRows { get; set; }
	public int ValidRows { get; set; }
	public int InvalidRows { get; set; }
	public int CreateCount { get; set; }
	public int ReplaceCount { get; set; }
	public int ProcessedRows { get; set; }
	public int CreatedCount { get; set; }
	public int ReplacedCount { get; set; }
	public bool CanOpen { get; set; }

	public AttendanceImportRunSource RunSource { get; set; } = AttendanceImportRunSource.FileUpload;

	public string? ExternalCorrelationId { get; set; }
	public string? IntegrationProvider { get; set; }
	public bool IsBatchLocked { get; set; }
}

public class AttendanceImportLogDetailDto : AttendanceImportLogListItemDto
{
	public DateTime? ConfirmedAt { get; set; }
	public DateTime? PreviewTokenExpiresAt { get; set; }
	public List<string> ErrorsSummary { get; set; } = [];
	public List<string> WarningsSummary { get; set; } = [];
}

public class AttendanceImportRowDto
{
	public Guid Id { get; set; }
	public Guid? EmployeeId { get; set; }
	public int RowNumber { get; set; }
	public string Action { get; set; } = "invalid";
	public string AttendanceDate { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string? EmployeeName { get; set; }
	public string? ContactNo { get; set; }
	public string? TimeIn { get; set; }
	public string? TimeOut { get; set; }
	public string? Status { get; set; }
	public AttendanceImportRowStatusCode? StatusCode { get; set; }
	public string? Remarks { get; set; }
	public List<string> Errors { get; set; } = [];
	public List<string> Warnings { get; set; } = [];
}
