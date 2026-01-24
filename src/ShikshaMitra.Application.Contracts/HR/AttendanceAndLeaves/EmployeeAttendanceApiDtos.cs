using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// Attendance status enumeration.
/// </summary>
public enum AttendanceStatus
{
	Present = 0,
	Halfday = 1,
	Absent = 2,
	Leave = 3,
	Pending = 4
}

/// <summary>
/// Paginated response for employee attendance records.
/// </summary>
public class PaginatedAttendanceResponse
{
	public DateTimeOffset Date { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public int TotalRecords { get; set; }
	public int TotalPages { get; set; }
	public List<AttendanceItem> Items { get; set; } = [];
}

/// <summary>
/// Individual attendance item in a paginated response.
/// </summary>
public class AttendanceItem
{
	public string EmployeeName { get; set; } = string.Empty;
	public Guid EmployeeId { get; set; }
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public string? PhoneNo { get; set; }
	public DateTimeOffset? InTime { get; set; }
	public DateTimeOffset? OutTime { get; set; }
	
	/// <summary>
	/// Hours worked formatted as "HHh MMm" (e.g., "08h 30m").
	/// </summary>
	public string? HoursWorked { get; set; }
	
	public AttendanceStatus Status { get; set; }
}

/// <summary>
/// Request DTO for updating employee attendance.
/// </summary>
public class UpdateAttendanceRequest
{
	[Required]
	public Guid EmployeeId { get; set; }

	[Required]
	public DateTimeOffset Date { get; set; }

	/// <summary>
	/// Clock-in time (required).
	/// </summary>
	[Required]
	public DateTimeOffset InTime { get; set; }

	/// <summary>
	/// Clock-out time (optional - employee may still be working).
	/// </summary>
	public DateTimeOffset? OutTime { get; set; }
}

/// <summary>
/// Generic success message response.
/// </summary>
public class AttendanceSuccessResponse
{
	public string Message { get; set; } = "Operation completed successfully";
}

/// <summary>
/// Response for attendance import operations.
/// </summary>
public class AttendanceImportResponse
{
	public DateTimeOffset Date { get; set; }
	public ImportSummary Summary { get; set; } = new();
	public List<ImportRowResult> Results { get; set; } = [];
}

/// <summary>
/// Summary statistics for an import operation.
/// </summary>
public class ImportSummary
{
	public int TotalRows { get; set; }
	public int SuccessfulRows { get; set; }
	public int FailedRows { get; set; }
	public int WarningCount { get; set; }
	public double ProcessingTimeSeconds { get; set; }
	public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result for each row in an import file.
/// </summary>
public class ImportRowResult
{
	public int RowNumber { get; set; }
	public bool IsSuccess { get; set; }
	public string? EmployeeCode { get; set; }
	public string? EmployeeName { get; set; }
	public List<ValidationMessage> Messages { get; set; } = [];
}

/// <summary>
/// Validation message with context for import operations.
/// </summary>
public class ValidationMessage
{
	public ValidationSeverity Severity { get; set; }
	public string FieldName { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string? Suggestion { get; set; }
	public string? ExpectedValue { get; set; }
	public string? ActualValue { get; set; }
}

/// <summary>
/// Severity level for validation messages.
/// </summary>
public enum ValidationSeverity
{
	/// <summary>Error - prevents row from being imported.</summary>
	Error = 0,
	/// <summary>Warning - row is imported but with concerns.</summary>
	Warning = 1,
	/// <summary>Info - informational message.</summary>
	Info = 2
}

/// <summary>
/// Input for getting paginated attendance by date.
/// </summary>
public class GetPaginatedAttendanceInput
{
	[Required]
	public DateTimeOffset Date { get; set; }
	
	/// <summary>
	/// Page index (0-based).
	/// </summary>
	[Range(0, int.MaxValue)]
	public int Page { get; set; } = 0;
	
	/// <summary>
	/// Number of records per page.
	/// </summary>
	[Range(10, 100)]
	public int PageSize { get; set; } = 20;
}

