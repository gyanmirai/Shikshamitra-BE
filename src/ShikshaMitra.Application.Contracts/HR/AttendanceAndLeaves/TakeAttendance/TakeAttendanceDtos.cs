using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

public class GetTakeAttendanceDayInput : PagedAndSortedResultRequestDto
{
	public string AttendanceDate { get; set; } = string.Empty;
	public Guid EducationMediumStageId { get; set; }
	public string? Department { get; set; }
	public bool? IsTeaching { get; set; }
	public Guid? RoleId { get; set; }
	public string? Search { get; set; }
}

public class TakeAttendanceDayResponse
{
	public int TotalCount { get; set; }
	public DateOnly Date { get; set; }
	public bool IsDayLocked { get; set; }
	public Guid? LockedByUserId { get; set; }
	public DateTime? LockedAt { get; set; }
	public TakeAttendanceSummaryDto Summary { get; set; } = new();
	public IReadOnlyList<TakeAttendanceEmployeeRowDto> Items { get; set; } = Array.Empty<TakeAttendanceEmployeeRowDto>();
}

public class TakeAttendanceSummaryDto
{
	public int Present { get; set; }
	public int HalfDay { get; set; }
	public int Absent { get; set; }
	public int NotMarked { get; set; }
}

public class TakeAttendanceEmployeeRowDto
{
	public Guid EmployeeId { get; set; }
	public string FullName { get; set; } = string.Empty;
	public string EmployeeCode { get; set; } = string.Empty;
	public string? Department { get; set; }
	public string DateIso { get; set; } = string.Empty;
	public string? InTime { get; set; }
	public string? OutTime { get; set; }
	public string? Remarks { get; set; }
	public List<string> Warnings { get; set; } = [];
	public List<string> Errors { get; set; } = [];
	public string StatusWire { get; set; } = "NOT_MARKED";
}

public class TakeAttendanceSaveInput
{
	public string AttendanceDate { get; set; } = string.Empty;
	public Guid EducationMediumStageId { get; set; }
	public List<TakeAttendanceSaveEntryDto> Entries { get; set; } = [];
}

public class TakeAttendanceSaveEntryDto
{
	public Guid EmployeeId { get; set; }
	public string StatusWire { get; set; } = "PRESENT";
	public string? InTime { get; set; }
	public string? OutTime { get; set; }
	public string? Remarks { get; set; }
}

public class TakeAttendanceSaveOutput
{
	public int UpsertedCount { get; set; }
}