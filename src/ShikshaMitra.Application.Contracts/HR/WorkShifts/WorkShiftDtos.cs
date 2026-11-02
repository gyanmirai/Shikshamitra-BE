using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.WorkShifts;

public class EmployeeWorkShiftDto : EntityDto<Guid>
{
	public string ShiftName { get; set; } = string.Empty;
	public TimeOnly StartTime { get; set; }
	public TimeOnly EndTime { get; set; }
	public bool IsActive { get; set; }
	public bool IsAssigned { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}
public class SaveWorkShiftInput
{
	[Required, StringLength(128)] public string ShiftName { get; set; } = string.Empty;
	public TimeOnly StartTime { get; set; }
	public TimeOnly EndTime { get; set; }
	public bool IsActive { get; set; } = true;
	public string? ConcurrencyStamp { get; set; }
}
public class EmployeeShiftAssignmentDto : EntityDto<Guid>
{
	public Guid WorkShiftId { get; set; }
	public DateOnly EffectiveFrom { get; set; }
	public DateOnly? EffectiveTo { get; set; }
	public string ShiftName { get; set; } = string.Empty;
	public TimeOnly StartTime { get; set; }
	public TimeOnly EndTime { get; set; }
	public string Provenance { get; set; } = string.Empty;
	public string ConcurrencyStamp { get; set; } = string.Empty;
}
public class EmployeeWorkScheduleDto
{
	public DateOnly Today { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public EmployeeShiftAssignmentDto? Current { get; set; }
	public List<EmployeeShiftAssignmentDto> Upcoming { get; set; } = [];
	public List<EmployeeShiftAssignmentDto> History { get; set; } = [];
}
public class ScheduleEmployeeShiftInput
{
	public Guid WorkShiftId { get; set; }
	public DateOnly EffectiveFrom { get; set; }
	[Required] public string ConcurrencyStamp { get; set; } = string.Empty;
}
public class InitialWorkShiftInput
{
	public Guid WorkShiftId { get; set; }
	public DateOnly EffectiveFrom { get; set; }
}

public class WorkShiftOptionsDto
{
	public DateOnly Today { get; set; }
	public List<EmployeeWorkShiftDto> Items { get; set; } = [];
}