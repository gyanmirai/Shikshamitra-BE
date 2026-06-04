using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;

namespace ShikshaMitra.HR.Policies;

internal static partial class HrPolicyMapping
{
	public static WorkCalendarHolidaySyncMode ParseHolidaySyncMode(string value) => Normalize(value) switch
	{
		"snapshot" => WorkCalendarHolidaySyncMode.Snapshot,
		"independent" => WorkCalendarHolidaySyncMode.Independent,
		_ => WorkCalendarHolidaySyncMode.LinkedWithOverrides
	};

	public static string MapHolidaySyncMode(WorkCalendarHolidaySyncMode value) => value switch
	{
		WorkCalendarHolidaySyncMode.Snapshot => "snapshot",
		WorkCalendarHolidaySyncMode.Independent => "independent",
		_ => "linked_with_overrides"
	};

	public static WorkCalendarHolidaySource ParseWorkCalendarHolidaySource(string? value) => Normalize(value) switch
	{
		"manual" => WorkCalendarHolidaySource.Manual,
		_ => WorkCalendarHolidaySource.CentralCalendar
	};

	public static string MapWorkCalendarHolidaySource(WorkCalendarHolidaySource value) => value switch
	{
		WorkCalendarHolidaySource.Manual => "manual",
		_ => "central_calendar"
	};

	public static AttendanceWeeklyOffPattern ParseWeeklyOffPattern(string value) => Normalize(value) switch
	{
		"sunday" => AttendanceWeeklyOffPattern.Sunday,
		"custom" => AttendanceWeeklyOffPattern.Custom,
		_ => AttendanceWeeklyOffPattern.SaturdaySunday
	};

	public static string MapWeeklyOffPattern(AttendanceWeeklyOffPattern value) => value switch
	{
		AttendanceWeeklyOffPattern.Sunday => "sunday",
		AttendanceWeeklyOffPattern.Custom => "custom",
		_ => "saturday_sunday"
	};

	public static WorkCalendarPolicyDto ToDto(WorkCalendarPolicy entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		Name = entity.Name,
		Status = MapPolicyStatus(entity.Status),
		EffectiveFrom = entity.EffectiveFrom,
		EffectiveTo = entity.EffectiveTo,
		WeeklyOffPattern = MapWeeklyOffPattern(entity.WeeklyOffPattern),
		CustomWeeklyOffs = entity.GetCustomWeeklyOffs().ToList(),
		HolidaySyncMode = MapHolidaySyncMode(entity.HolidaySyncMode),
		Version = entity.Version,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId
	};

	public static WorkCalendarHolidayDto ToDto(WorkCalendarHoliday entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		WorkCalendarPolicyId = entity.WorkCalendarPolicyId,
		Date = entity.Date,
		FromDate = entity.FromDate,
		ToDate = entity.ToDate,
		DayCount = (entity.ToDate.Date - entity.FromDate.Date).Days + 1,
		HolidayName = entity.HolidayName,
		Source = MapWorkCalendarHolidaySource(entity.Source),
		CalendarHolidayId = entity.CalendarHolidayId,
		IsIncluded = entity.IsIncluded,
		Notes = entity.Notes
	};

	public static WorkCalendarHolidaySummaryDto BuildHolidaySummary(IEnumerable<WorkCalendarHolidayDto> holidays)
	{
		var rows = holidays.ToList();
		return new WorkCalendarHolidaySummaryDto
		{
			TotalCount = rows.Count,
			IncludedCount = rows.Count(x => x.IsIncluded),
			RemovedCount = rows.Count(x => !x.IsIncluded),
			ManualCount = rows.Count(x => x.Source == "manual"),
			CentralCount = rows.Count(x => x.Source == "central_calendar")
		};
	}
}
