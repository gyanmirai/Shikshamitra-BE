using ShikshaMitra.Students.Attendance;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// DTO for getting students list for attendance marking
/// </summary>
public class GetStudentsForAttendanceInput
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
/// DTO representing a student with attendance status
/// </summary>
public class StudentAttendanceDto
{
	public Guid StudentId { get; set; }
	public int? GrNo { get; set; }
	public int? RollNo { get; set; }
	public string? FirstName { get; set; }
	public string? MiddleName { get; set; }
	public string? LastName { get; set; }
	public string FullName => $"{FirstName} {MiddleName} {LastName}".Trim();
	public string? OrganizationName { get; set; }
	public string? ClassName { get; set; }
	public string? DivisionName { get; set; }
	public AttendanceStatus Status { get; set; } = AttendanceStatus.Pending;
	public int PresentDays { get; set; }
	public double AttendancePercentage { get; set; }
	public string? Remarks { get; set; }
}

/// <summary>
/// Response with students list and summary counters
/// </summary>
public class StudentAttendanceListDto
{
	public Guid? AttendanceDayId { get; set; }
	public DateTime Date { get; set; }
	public bool IsLocked { get; set; }
	public int TotalStudents { get; set; }
	public int PresentCount { get; set; }
	public int AbsentCount { get; set; }
	public int PendingCount { get; set; }
	public List<StudentAttendanceDto> Students { get; set; } = new();
}

public class GetStudentAttendanceHistoryInput : PagedAndSortedResultRequestDto
{
	[Required]
	public Guid StudentId { get; set; }

	public DateTime? StartDate { get; set; }
	public DateTime? EndDate { get; set; }
}

public class StudentAttendanceHistoryDto
{
	public Guid AttendanceDayId { get; set; }
	public DateTime Date { get; set; }
	public AttendanceStatus Status { get; set; }
	public string? Remarks { get; set; }
}
