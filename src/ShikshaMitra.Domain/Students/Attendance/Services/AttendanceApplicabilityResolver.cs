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
public sealed class AttendanceApplicabilityContext(AttendancePolicyScope scope, IReadOnlyDictionary<DateTime, AttendanceApplicabilityResult> days)
{
	public AttendancePolicyScope Scope { get; } = scope;
	public AttendanceApplicabilityResult For(Guid enrollmentId, DateTime date, DateTime? currentSchoolDate = null)
	{
		var result = days.GetValueOrDefault(date.Date) ?? new(false, "Calendar not configured", IsProvisional: true);
		if (date.Date == currentSchoolDate?.Date && result.IsProvisional &&
			result.Reason is ("Calendar not configured" or "Working day not configured"))
			return new(true, "Current school day (calendar setup pending)", IsProvisional: true);
		return result;
	}
}

public class AttendanceApplicabilityResolver(IRepository<AcademicCalendar, Guid> calendars, IRepository<AcademicCalendarDay, Guid> days,
	IRepository<AcademicCalendarSetting, Guid> settings,
	IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions, IRepository<EducationMediumStage, Guid> stages,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationBoard, Guid> boards) : DomainService
{
	public async Task<AttendanceApplicabilityContext> LoadAsync(Guid academicYearId, Guid standardId, Guid divisionId,
		DateTime startDate, DateTime endDate, IReadOnlyCollection<Guid> enrollmentIds) =>
		(await LoadManyAsync(academicYearId, [(standardId, divisionId)], startDate, endDate))[divisionId];

	public async Task<IReadOnlyDictionary<Guid, AttendanceApplicabilityContext>> LoadManyAsync(Guid academicYearId,
		IReadOnlyCollection<(Guid StandardId, Guid DivisionId)> paths, DateTime startDate, DateTime endDate)
	{
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(AttendancePolicyErrorCodes.TenantRequired);
		if (endDate < startDate || (endDate.Date - startDate.Date).TotalDays > 1096) throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
		var divisionIds = paths.Select(p => p.DivisionId).Distinct().ToArray();
		var hierarchy = from division in await divisions.GetQueryableAsync()
			join standard in await standards.GetQueryableAsync() on division.StandardId equals standard.Id
			join stage in await stages.GetQueryableAsync() on standard.EducationMediumStageId equals stage.Id
			join medium in await mediums.GetQueryableAsync() on stage.EducationMediumId equals medium.Id
			join board in await boards.GetQueryableAsync() on medium.EducationBoardId equals board.Id
			where Enumerable.Contains(divisionIds, division.Id) && board.AcademicYearId == academicYearId &&
				division.TenantId == CurrentTenant.Id && standard.TenantId == CurrentTenant.Id && stage.TenantId == CurrentTenant.Id &&
				medium.TenantId == CurrentTenant.Id && board.TenantId == CurrentTenant.Id
			select new AttendancePolicyScope(board.Id, medium.Id, stage.Id, standard.Id, division.Id);
		var scopes = await AsyncExecuter.ToListAsync(hierarchy);
		if (paths.Any(path => !scopes.Any(scope => scope.StandardId == path.StandardId && scope.DivisionId == path.DivisionId)))
			throw new BusinessException(AttendancePolicyErrorCodes.InvalidPolicy);
		var mediumIds = scopes.Select(s => s.MediumId).ToArray();
		var calendarList = await AsyncExecuter.ToListAsync((await calendars.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.Status == CalendarStatus.Published && (x.AcademicYearId == academicYearId || x.AcademicYearId == null) &&
			(Enumerable.Contains(mediumIds, x.AcademicMediumId) || x.AcademicMediumId == null) && x.StartDate <= endDate.Date && x.EndDate >= startDate.Date));
		var calendarIds = calendarList.Select(x => x.Id).ToArray();
		var dayList = await AsyncExecuter.ToListAsync((await days.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(calendarIds, x.AcademicCalendarId) &&
			x.Date >= startDate.Date && x.Date <= endDate.Date && x.Status != CalendarDayStatus.Cancelled));
		var settingList = await AsyncExecuter.ToListAsync((await settings.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			(x.AcademicYearId == academicYearId || x.AcademicYearId == null) && (Enumerable.Contains(mediumIds, x.AcademicMediumId) || x.AcademicMediumId == null)));
		var result = new Dictionary<Guid, AttendanceApplicabilityContext>();
		foreach (var scope in scopes)
		{
			var calendarSetting = settingList.Where(x => x.AcademicMediumId == scope.MediumId || x.AcademicMediumId == null)
				.OrderByDescending(x => x.AcademicYearId.HasValue).ThenByDescending(x => x.AcademicMediumId.HasValue).FirstOrDefault();
			var applicableCalendars = calendarList.Where(x => x.AcademicMediumId == scope.MediumId || x.AcademicMediumId == null).ToList();
			var applicableDays = dayList.Where(x => x.AppliesToAll || (!x.StandardId.HasValue || x.StandardId == scope.StandardId) && (!x.DivisionId.HasValue || x.DivisionId == scope.DivisionId)).ToList();
			var resolved = new Dictionary<DateTime, AttendanceApplicabilityResult>();
			for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
				resolved[date] = AttendanceCalendarDayEvaluator.Resolve(date, applicableCalendars, applicableDays, calendarSetting);
			result[scope.DivisionId!.Value] = new(scope, resolved);
		}
		return result;
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
