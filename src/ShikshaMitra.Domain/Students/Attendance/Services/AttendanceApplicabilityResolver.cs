using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.Attendance;

public sealed record AttendanceApplicabilityResult(bool IsExpected, string Reason, string? ScheduleLabel = null, bool IsProvisional = false);

/// <summary>A bounded, request-local snapshot. No per-student database queries are made when calculating a register.</summary>
public sealed class AttendanceApplicabilityContext(AttendancePolicyScope scope, IReadOnlyDictionary<DateTime, AttendanceApplicabilityResult> days,
	IReadOnlyList<StudentAttendanceSchedule> schedules)
{
	public AttendancePolicyScope Scope { get; } = scope;
	public AttendanceApplicabilityResult For(Guid enrollmentId, DateTime date)
	{
		var day = days.GetValueOrDefault(date.Date) ?? new(false, "Calendar not configured", IsProvisional: true);
		if (!day.IsExpected) return day;
		var schedule = schedules.Where(x => x.EnrollmentId == enrollmentId && x.InRange(date))
			.OrderByDescending(x => x.Kind == "Temporary").ThenByDescending(x => x.StartDate).FirstOrDefault();
		if (schedule == null) return day;
		var label = schedule.StartTime == null ? schedule.Name : $"{schedule.Name} ({schedule.StartTime}–{schedule.EndTime})";
		return schedule.Expected(date) ? new(true, "Individual schedule", label) : new(false, "Not scheduled", label);
	}
}

public class AttendanceApplicabilityResolver(IRepository<AcademicCalendar, Guid> calendars, IRepository<AcademicCalendarDay, Guid> days,
	IRepository<AcademicCalendarSetting, Guid> settings, IRepository<StudentAttendanceSchedule, Guid> schedules,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions, IRepository<EducationMediumStage, Guid> stages,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationBoard, Guid> boards) : DomainService
{
	public async Task<AttendanceApplicabilityContext> LoadAsync(Guid academicYearId, Guid standardId, Guid divisionId,
		DateTime startDate, DateTime endDate, IReadOnlyCollection<Guid> enrollmentIds)
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(AttendancePolicyErrorCodes.TenantRequired);
		if (endDate < startDate || (endDate.Date - startDate.Date).TotalDays > 1096) throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
		var hierarchy = from division in await divisions.GetQueryableAsync()
			join standard in await standards.GetQueryableAsync() on division.StandardId equals standard.Id
			join stage in await stages.GetQueryableAsync() on standard.EducationMediumStageId equals stage.Id
			join medium in await mediums.GetQueryableAsync() on stage.EducationMediumId equals medium.Id
			join board in await boards.GetQueryableAsync() on medium.EducationBoardId equals board.Id
			where division.Id == divisionId && standard.Id == standardId && board.AcademicYearId == academicYearId &&
				division.TenantId == CurrentTenant.Id && standard.TenantId == CurrentTenant.Id && stage.TenantId == CurrentTenant.Id &&
				medium.TenantId == CurrentTenant.Id && board.TenantId == CurrentTenant.Id
			select new AttendancePolicyScope(board.Id, medium.Id, stage.Id, standard.Id, division.Id);
		var scope = await AsyncExecuter.SingleOrDefaultAsync(hierarchy) ?? throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
		var calendarQuery = await calendars.GetQueryableAsync();
		var calendarList = await AsyncExecuter.ToListAsync(calendarQuery.Where(x => x.TenantId == CurrentTenant.Id &&
			x.Status == CalendarStatus.Published && (x.AcademicYearId == academicYearId || x.AcademicYearId == null) &&
			(x.AcademicMediumId == scope.MediumId || x.AcademicMediumId == null) && x.StartDate <= endDate.Date && x.EndDate >= startDate.Date));
		var calendarIds = calendarList.Select(x => x.Id).ToArray();
		var dayQuery = await days.GetQueryableAsync();
		var dayList = await AsyncExecuter.ToListAsync(dayQuery.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(calendarIds, x.AcademicCalendarId) &&
			x.Date >= startDate.Date && x.Date <= endDate.Date && x.Status != CalendarDayStatus.Cancelled &&
			(x.AppliesToAll || ((!x.StandardId.HasValue || x.StandardId == standardId) && (!x.DivisionId.HasValue || x.DivisionId == divisionId)))));
		var settingQuery = await settings.GetQueryableAsync();
		var settingList = await AsyncExecuter.ToListAsync(settingQuery.Where(x => x.TenantId == CurrentTenant.Id &&
			(x.AcademicYearId == academicYearId || x.AcademicYearId == null) && (x.AcademicMediumId == scope.MediumId || x.AcademicMediumId == null)));
		var calendarSetting = settingList.OrderByDescending(x => x.AcademicYearId.HasValue).ThenByDescending(x => x.AcademicMediumId.HasValue).FirstOrDefault();
		var scheduleQuery = await schedules.GetQueryableAsync(); var ids = enrollmentIds.ToArray();
		var scheduleList = await AsyncExecuter.ToListAsync(scheduleQuery.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId &&
			Enumerable.Contains(ids, x.EnrollmentId) && x.StartDate <= endDate.Date && (!x.EndDate.HasValue || x.EndDate >= startDate.Date)));
		var resolved = new Dictionary<DateTime, AttendanceApplicabilityResult>();
		for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
		{
			resolved[date] = AttendanceCalendarDayEvaluator.Resolve(date, calendarList, dayList, calendarSetting);
		}
		return new(scope, resolved, scheduleList);
	}
}

/// <summary>Resolves the existing Academic Calendar's published days; attendance owns no holiday definitions.</summary>
public static class AttendanceCalendarDayEvaluator
{
	public static AttendanceApplicabilityResult Resolve(DateTime date, IReadOnlyList<AcademicCalendar> calendars,
		IReadOnlyList<AcademicCalendarDay> days, AcademicCalendarSetting? setting)
	{
		var applicable = calendars.Where(x => x.Status == CalendarStatus.Published && x.StartDate.Date <= date.Date && x.EndDate.Date >= date.Date)
			.OrderByDescending(x => x.AcademicYearId.HasValue).ThenByDescending(x => x.AcademicMediumId.HasValue).ThenByDescending(x => x.IsDefault).ToList();
		if (applicable.Count == 0) return new(false, "Calendar not configured", IsProvisional: true);
		var selected = applicable[0];
		if (applicable.Skip(1).Any(x => x.AcademicYearId.HasValue == selected.AcademicYearId.HasValue &&
			x.AcademicMediumId.HasValue == selected.AcademicMediumId.HasValue && x.IsDefault == selected.IsDefault))
			return new(false, "Conflicting published calendars", IsProvisional: true);
		var explicitDays = days.Where(x => x.AcademicCalendarId == selected.Id && x.Date.Date == date.Date && x.Status != CalendarDayStatus.Cancelled)
			.Where(x => x.DayType is DayType.WorkingDay or DayType.Holiday or DayType.Vacation or DayType.HalfDay or DayType.EmergencyClosure)
			.OrderByDescending(x => x.DivisionId.HasValue ? 2 : x.StandardId.HasValue ? 1 : 0)
			.ThenByDescending(x => x.DayType == DayType.EmergencyClosure ? 3 : x.DayType is DayType.Holiday or DayType.Vacation ? 2 : 1).ToList();
		var day = explicitDays.FirstOrDefault(x => x.DayType == DayType.EmergencyClosure) ?? explicitDays.FirstOrDefault();
		if (day != null) return new(day.DayType is DayType.WorkingDay or DayType.HalfDay ||
			day.DayType == DayType.Holiday && setting?.AutoExemptAttendanceOnHoliday == false ||
			day.DayType == DayType.Vacation && setting?.AutoExemptAttendanceOnVacation == false, day.Title);
		if (setting == null || setting.WeekendRule == WeekendRule.Custom) return new(false, "Working day not configured", IsProvisional: true);
		var weekend = date.DayOfWeek == DayOfWeek.Sunday || (date.DayOfWeek == DayOfWeek.Saturday &&
			(setting.WeekendRule == WeekendRule.SaturdaySunday || (setting.WeekendRule == WeekendRule.AlternateSaturday && ((date.Day - 1) / 7 + 1) % 2 == 0)));
		return new(!weekend, weekend ? "Weekend" : "School day");
	}
}
