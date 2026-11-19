using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Students.Records;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.Attendance;

public class AttendanceWorkspaceInput : PagedAndSortedResultRequestDto
{
	[Required] public Guid AcademicYearId { get; set; }
	[Required] public DateTime Date { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	[StringLength(150)] public string? Search { get; set; }
}

public class AttendanceAssignedClassDto
{
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
}

public class AttendanceWorkspaceContextDto
{
	public bool IsAdmin { get; set; }
	public DateTime SchoolToday { get; set; }
	public bool CanManageLock { get; set; }
	public List<AttendanceAssignedClassDto> AssignedClasses { get; set; } = [];
	public StudentDirectoryOptionsDto Options { get; set; } = new();
}
