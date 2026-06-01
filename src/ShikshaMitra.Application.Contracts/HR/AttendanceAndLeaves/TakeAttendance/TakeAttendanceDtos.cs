using System;
using System.Collections.Generic;
using FluentValidation;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;

public enum TakeAttendanceStatus
{
	NotMarked = 0,
	Present = 1,
	Absent = 2,
	HalfDay = 3
}

public class GetTakeAttendanceDayInput : PagedAndSortedResultRequestDto
{
	public string AttendanceDate { get; set; } = string.Empty;
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
	public bool IsHoliday { get; set; }
	public string? HolidayName { get; set; }
	public string? HolidayReason { get; set; }
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
	public string? ShiftName { get; set; }
	public string? ShiftStartTime { get; set; }
	public string? ShiftEndTime { get; set; }
	public string? Remarks { get; set; }
	public List<string> Warnings { get; set; } = [];
	public List<string> Errors { get; set; } = [];
	public bool IsHoliday { get; set; }
	public string? HolidayName { get; set; }
	public string? HolidayReason { get; set; }
	public TakeAttendanceStatus Status { get; set; } = TakeAttendanceStatus.NotMarked;
}

public class TakeAttendanceSaveInput
{
	public string AttendanceDate { get; set; } = string.Empty;
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

public class TakeAttendanceSaveInputValidator : AbstractValidator<TakeAttendanceSaveInput>
{
	public TakeAttendanceSaveInputValidator()
	{
		RuleFor(x => x.AttendanceDate).NotEmpty().MaximumLength(30);
		RuleFor(x => x.Entries).NotNull().Must(x => x.Count > 0);
		RuleForEach(x => x.Entries).SetValidator(new TakeAttendanceSaveEntryDtoValidator());
	}
}

public class TakeAttendanceSaveEntryDtoValidator : AbstractValidator<TakeAttendanceSaveEntryDto>
{
	public TakeAttendanceSaveEntryDtoValidator()
	{
		RuleFor(x => x.EmployeeId).NotEmpty();
		RuleFor(x => x.StatusWire).NotEmpty().MaximumLength(20);
		RuleFor(x => x.InTime).MaximumLength(40);
		RuleFor(x => x.OutTime).MaximumLength(40);
		RuleFor(x => x.Remarks).MaximumLength(500);
	}
}
