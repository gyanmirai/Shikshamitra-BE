using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.AttendanceAndLeaves.EmployeeAttendanceContract;

#region Enums

/// <summary>
/// Attendance status for employee attendance (matches OpenAPI contract)
/// </summary>
public enum AttendanceStatus
{
	Present,
	Halfday,
	Absent,
	Leave,
	Pending
}

#endregion

#region GET /attendance/employees Response DTOs

/// <summary>
/// Response for GET /attendance/employees
/// </summary>
public class PaginatedAttendanceResponse
{
	public DateTimeOffset Date { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public int TotalRecords { get; set; }
	public int TotalPages { get; set; }
	public List<AttendanceItem> Items { get; set; } = new();
}

/// <summary>
/// Individual attendance item
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
	/// Hours worked formatted as "HHh MMm" (e.g., "08h 30m")
	/// </summary>
	public string? HoursWorked { get; set; }
	
	public AttendanceStatus Status { get; set; }
}

#endregion

#region PUT /attendance/employees Request DTO

/// <summary>
/// Request for PUT /attendance/employees
/// </summary>
public class UpdateAttendanceRequest
{
	[Required]
	public Guid EmployeeId { get; set; }

	[Required]
	public DateTimeOffset Date { get; set; }

	/// <summary>
	/// In time is required for attendance
	/// </summary>
	[Required]
	public DateTimeOffset InTime { get; set; }

	/// <summary>
	/// Out time is optional - employee may still be working
	/// </summary>
	public DateTimeOffset? OutTime { get; set; }
}

/// <summary>
/// Success message response
/// </summary>
public class SuccessMessage
{
	public string Message { get; set; } = "Attendance updated successfully";
}

#endregion

#region POST /attendance/import Response DTOs

/// <summary>
/// Enhanced response for POST /attendance/import with detailed error tracking
/// </summary>
public class AttendanceImportResponse
{
	public DateTimeOffset Date { get; set; }
	public ImportSummary Summary { get; set; } = new();
	public List<ImportRowResult> Results { get; set; } = new();
}

/// <summary>
/// Summary statistics for the import operation
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
/// Result for each row in the import file
/// </summary>
public class ImportRowResult
{
	public int RowNumber { get; set; }
	public bool IsSuccess { get; set; }
	public string? EmployeeCode { get; set; }
	public string? EmployeeName { get; set; }
	public List<ValidationMessage> Messages { get; set; } = new();
}

/// <summary>
/// Individual validation message with context
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
/// Severity level for validation messages
/// </summary>
public enum ValidationSeverity
{
	/// <summary>Error - prevents row from being imported</summary>
	Error,
	/// <summary>Warning - row is imported but with concerns</summary>
	Warning,
	/// <summary>Info - informational message</summary>
	Info
}

/// <summary>
/// Legacy error format for backward compatibility
/// </summary>
[Obsolete("Use ImportRowResult with ValidationMessage instead")]
public class ImportError
{
	public int RowNumber { get; set; }
	public string Reason { get; set; } = string.Empty;
}

#endregion

#region Input DTOs

/// <summary>
/// Input for getting paginated attendance by date
/// </summary>
public class GetPaginatedAttendanceInput
{
	[Required]
	public DateTimeOffset Date { get; set; }
	
	/// <summary>
	/// 0-based page index
	/// </summary>
	[Range(0, int.MaxValue)]
	public int Page { get; set; } = 0;
	
	[Range(10, 100)]
	public int PageSize { get; set; } = 20;
}

/// <summary>
/// Input for importing attendance from Excel
/// </summary>
public class ImportAttendanceInput
{
	[Required]
	public DateTimeOffset Date { get; set; }
	
	// Note: The IFormFile is passed separately in the controller due to ABP limitations
}

#endregion

#region Extended App Service Interface

public interface IEmployeeAttendanceContractAppService
{
	Task<PaginatedAttendanceResponse> GetPaginatedByDateAsync(GetPaginatedAttendanceInput input);
	Task<SuccessMessage> UpdateAttendanceAsync(UpdateAttendanceRequest input);
	Task<AttendanceImportResponse> ImportAttendanceAsync(
		DateTimeOffset date, 
		Stream fileStream, 
		string fileName,
		CancellationToken cancellationToken = default);
}

#endregion

