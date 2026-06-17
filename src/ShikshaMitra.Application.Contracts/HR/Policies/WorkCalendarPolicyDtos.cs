using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.HR.Policies;

public class WorkCalendarPolicyDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public string WeeklyOffPattern { get; set; } = "saturday_sunday";
	public List<int> CustomWeeklyOffs { get; set; } = [];
	public string HolidaySyncMode { get; set; } = "linked_with_overrides";
	public List<WorkCalendarHolidayDto> Holidays { get; set; } = [];
	public WorkCalendarHolidaySummaryDto HolidaySummary { get; set; } = new();
	public int Version { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
}

public class CreateUpdateWorkCalendarPolicyDto
{
	public Guid AcademicYearId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = "draft";
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public string WeeklyOffPattern { get; set; } = "saturday_sunday";
	public List<int> CustomWeeklyOffs { get; set; } = [];
	public string HolidaySyncMode { get; set; } = "linked_with_overrides";
	public List<WorkCalendarHolidayDto> Holidays { get; set; } = [];
}

public class WorkCalendarHolidayDto
{
	public Guid? Id { get; set; }
	public Guid? WorkCalendarPolicyId { get; set; }
	public Guid AcademicYearId { get; set; }
	public DateOnly Date { get; set; }
	public DateOnly? FromDate { get; set; }
	public DateOnly? ToDate { get; set; }
	public int DayCount { get; set; } = 1;
	public string HolidayName { get; set; } = string.Empty;
	public string Source { get; set; } = "central_calendar";
	public Guid? CalendarHolidayId { get; set; }
	public bool IsIncluded { get; set; } = true;
	public string? Notes { get; set; }
}

public class WorkCalendarHolidaySummaryDto
{
	public int TotalCount { get; set; }
	public int IncludedCount { get; set; }
	public int RemovedCount { get; set; }
	public int ManualCount { get; set; }
	public int CentralCount { get; set; }
}
