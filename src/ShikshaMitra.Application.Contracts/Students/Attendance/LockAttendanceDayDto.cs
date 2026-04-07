using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// DTO for locking/unlocking attendance day
/// </summary>
public class LockAttendanceDayInput
{
	[Required]
	public Guid AcademicYearId { get; set; }

	[Required]
	public Guid StandardId { get; set; }

	[Required]
	public Guid DivisionId { get; set; }

	[Required]
	public DateTime Date { get; set; }
}

/// <summary>
/// Response after locking/unlocking
/// </summary>
public class LockAttendanceDayOutput
{
	public Guid AttendanceDayId { get; set; }
	public bool IsLocked { get; set; }
	public string Message { get; set; } = string.Empty;
}
