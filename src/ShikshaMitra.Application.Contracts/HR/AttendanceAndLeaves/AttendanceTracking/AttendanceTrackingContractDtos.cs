using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;

#region Enums

/// <summary>
/// Attendance status for tracking purposes (matches OpenAPI contract)
/// </summary>
public enum AttendanceTrackingStatus
{
	PRESENT = 0,
	ABSENT = 1,
	HALF_DAY = 2,
	REJECTED_LEAVE = 3
}

#endregion

#region Date-Wise Response DTOs

/// <summary>
/// Response for GET /api/attendance-tracking/date-wise
/// </summary>
public class AttendanceDateWiseResponse
{
	public int TotalCount { get; set; }
	public int CurrentPage { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
	public AttendanceSummary Summary { get; set; } = new();
	public List<AttendanceDateWiseItem> Items { get; set; } = new();
}

/// <summary>
/// Individual attendance item for date-wise response
/// </summary>
public class AttendanceDateWiseItem
{
	public string EmployeeId { get; set; } = string.Empty;
	public string FullName { get; set; } = string.Empty;
	public string EmployeeIdentifier { get; set; } = string.Empty;
	public string ContactNo { get; set; } = string.Empty;

	/// <summary>
	/// In time formatted as HH:mm (e.g., "09:10")
	/// </summary>
	public string? InTime { get; set; }

	/// <summary>
	/// Out time formatted as HH:mm (e.g., "17:45")
	/// </summary>
	public string? OutTime { get; set; }

	/// <summary>
	/// Hours worked formatted as HH:mm (e.g., "08:35")
	/// </summary>
	public string? HoursWorked { get; set; }

	public string? Remarks { get; set; }
	public AttendanceTrackingStatus Status { get; set; }
}

#endregion

#region Employee-Wise Response DTOs

/// <summary>
/// Response for GET /api/attendance-tracking/employee-wise
/// </summary>
public class AttendanceEmployeeWiseResponse
{
	public AttendanceSummary AttendanceSummary { get; set; } = new();
	public List<AttendanceEmployeeWiseItem> Items { get; set; } = new();
}

/// <summary>
/// Individual attendance item for employee-wise response
/// </summary>
public class AttendanceEmployeeWiseItem
{
	/// <summary>
	/// Date formatted as DD-MM-YYYY
	/// </summary>
	public string Date { get; set; } = string.Empty;

	/// <summary>
	/// Day of week (e.g., "Monday")
	/// </summary>
	public string Day { get; set; } = string.Empty;

	/// <summary>
	/// In time formatted as HH:mm AM/PM (e.g., "08:30 AM")
	/// </summary>
	public string? InTime { get; set; }

	/// <summary>
	/// Out time formatted as HH:mm AM/PM (e.g., "05:30 PM")
	/// </summary>
	public string? OutTime { get; set; }

	/// <summary>
	/// Hours worked formatted as HHH MMM (e.g., "09H 00M")
	/// </summary>
	public string? HoursWorked { get; set; }

	public string? Description { get; set; }
	public AttendanceTrackingStatus Status { get; set; }
}

#endregion

#region Summary DTO

/// <summary>
/// Summary of attendance counts
/// </summary>
public class AttendanceSummary
{
	public int All { get; set; }
	public int Present { get; set; }
	public int Absent { get; set; }
	public int HalfDay { get; set; }
	public int RejectedLeave { get; set; }
}

#endregion

#region Input DTOs

/// <summary>
/// Input for date-wise attendance query
/// </summary>
public class GetAttendanceDateWiseInput
{
	/// <summary>
	/// Attendance date (supports DD-MM-YYYY format and ISO date-time)
	/// </summary>
	[Required]
	public string AttendanceDate { get; set; } = string.Empty;

	/// <summary>
	/// Optional status filter
	/// </summary>
	public AttendanceTrackingStatus? Status { get; set; }

	[Range(1, int.MaxValue)]
	public int Page { get; set; } = 1;

	[Range(10, 100)]
	public int PageSize { get; set; } = 10;
}

/// <summary>
/// Input for employee-wise attendance query
/// </summary>
public class GetAttendanceEmployeeWiseInput
{
	[Required]
	public Guid EmployeeId { get; set; }

	/// <summary>
	/// Start date (DD-MM-YYYY format)
	/// </summary>
	[Required]
	public string StartDate { get; set; } = string.Empty;

	/// <summary>
	/// End date (DD-MM-YYYY format)
	/// </summary>
	[Required]
	public string EndDate { get; set; } = string.Empty;

	/// <summary>
	/// Optional status filter
	/// </summary>
	public AttendanceTrackingStatus? Status { get; set; }
}

#endregion

#region App Service Interface

public interface IAttendanceTrackingAppService
{
	Task<AttendanceDateWiseResponse> GetDateWiseAsync(GetAttendanceDateWiseInput input);
	Task<AttendanceEmployeeWiseResponse> GetEmployeeWiseAsync(GetAttendanceEmployeeWiseInput input);
}

#endregion

