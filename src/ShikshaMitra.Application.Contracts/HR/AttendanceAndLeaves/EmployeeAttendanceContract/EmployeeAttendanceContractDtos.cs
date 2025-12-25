using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
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
/// Response for POST /attendance/import
/// </summary>
public class AttendanceImportResponse
{
	public DateTimeOffset Date { get; set; }
	public int TotalRecords { get; set; }
	public int SuccessfulRecords { get; set; }
	public int FailedRecords { get; set; }
	public List<ImportError>? Errors { get; set; }
}

/// <summary>
/// Individual import error
/// </summary>
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
	Task<AttendanceImportResponse> ImportAttendanceAsync(DateTimeOffset date, Stream fileStream, string fileName);
}

#endregion

