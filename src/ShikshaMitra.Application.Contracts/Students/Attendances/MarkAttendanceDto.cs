using ShikshaMitra.Students.Attendance;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Attendances;

/// <summary>
/// DTO for marking attendance
/// </summary>
public class MarkAttendanceInput
{
	[Required]
	public Guid AcademicYearId { get; set; }

	[Required]
	public Guid StandardId { get; set; }

	[Required]
	public Guid DivisionId { get; set; }

	[Required]
	public DateTime Date { get; set; }

	[Required]
	[MinLength(1)]
	public List<AttendanceEntryDto> Entries { get; set; } = new();
}

/// <summary>
/// DTO for a single attendance entry
/// </summary>
public class AttendanceEntryDto
{
	[Required]
	public Guid StudentId { get; set; }

	[Required]
	public AttendanceStatus Status { get; set; }

	[MaxLength(500)]
	public string? Remarks { get; set; }
}

/// <summary>
/// Response after marking attendance
/// </summary>
public class MarkAttendanceOutput
{
	public Guid AttendanceDayId { get; set; }
	public bool IsLocked { get; set; }
	public int TotalMarked { get; set; }
	public int PresentCount { get; set; }
	public int AbsentCount { get; set; }
	public string Message { get; set; } = "Attendance marked successfully";
}
