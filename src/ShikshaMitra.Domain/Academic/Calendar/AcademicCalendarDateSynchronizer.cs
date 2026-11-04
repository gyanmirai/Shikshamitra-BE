using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarDateSynchronizer(
	IEducationBoardRepository boards,
	IRepository<EducationMedium, Guid> mediums,
	IRepository<AcademicCalendar, Guid> calendars,
	IRepository<AcademicCalendarDay, Guid> days,
	IRepository<AcademicCalendarSetting, Guid> settings,
	AcademicCalendarManager calendarManager) : DomainService
{
	public async Task SynchronizeBoardAsync(EducationBoard board, DateTime startDate, DateTime endDate)
	{
		if (endDate <= startDate) throw new ArgumentException("End date must be after start date.");
		var ranges = await GetCalendarRangesAsync(board, startDate, endDate);
		await ValidateAsync(ranges);
		await ApplyAsync(ranges);
	}

	public async Task SynchronizeYearAsync(AcademicYear year, DateTime startDate, DateTime endDate)
	{
		if (endDate <= startDate) throw new InvalidAcademicYearDatesException(startDate, endDate);
		var yearBoards = await boards.GetListAsync(board =>
			board.TenantId == CurrentTenant.Id && board.AcademicYearId == year.Id);
		var inheritedBoards = new List<EducationBoard>();
		var ranges = new List<CalendarRange>();
		foreach (var board in yearBoards)
		{
			var inheritsYear = board.StartDate.Date == year.StartDate.Date && board.EndDate.Date == year.EndDate.Date;
			var boardStart = inheritsYear ? startDate : board.StartDate;
			var boardEnd = inheritsYear ? endDate : board.EndDate;
			if (boardStart.Date < startDate.Date || boardEnd.Date > endDate.Date)
			{
				throw new BusinessException(AcademicCalendarConsts.BoardOutsideAcademicYear)
					.WithData("Board", board.Name);
			}
			if (inheritsYear) inheritedBoards.Add(board);
			ranges.AddRange(await GetCalendarRangesAsync(board, boardStart, boardEnd));
		}

		await ValidateAsync(ranges);
		await ApplyAsync(ranges);
		foreach (var board in inheritedBoards)
		{
			board.SetDates(startDate, endDate);
			await boards.UpdateAsync(board);
		}
	}

	private async Task<List<CalendarRange>> GetCalendarRangesAsync(EducationBoard board, DateTime startDate, DateTime endDate)
	{
		var boardMediums = await mediums.GetListAsync(medium =>
			medium.TenantId == CurrentTenant.Id && medium.EducationBoardId == board.Id);
		var mediumIds = boardMediums.Select(medium => medium.Id).ToArray();
		if (mediumIds.Length == 0) return [];
		var query = await calendars.WithDetailsAsync(calendar => calendar.Days);
		var boardCalendars = await AsyncExecuter.ToListAsync(query.Where(calendar =>
			calendar.TenantId == CurrentTenant.Id &&
			calendar.AcademicMediumId.HasValue && Enumerable.Contains(mediumIds, calendar.AcademicMediumId.Value) &&
			(!calendar.AcademicYearId.HasValue || calendar.AcademicYearId == board.AcademicYearId) &&
			calendar.Status != CalendarStatus.Archived));
		return boardCalendars
			.Where(calendar => calendar.StartDate.Date != startDate.Date || calendar.EndDate.Date != endDate.Date)
			.Select(calendar => new CalendarRange(calendar, startDate.Date, endDate.Date))
			.ToList();
	}

	private async Task ValidateAsync(List<CalendarRange> ranges)
	{
		foreach (var range in ranges)
		{
			if (await days.AnyAsync(day => day.TenantId == CurrentTenant.Id &&
				day.AcademicCalendarId == range.Calendar.Id && day.Status == CalendarDayStatus.Active &&
				(day.Date < range.StartDate || day.Date > range.EndDate)))
			{
				throw new BusinessException(AcademicCalendarConsts.DatesExcludeMarkers)
					.WithData("Calendar", range.Calendar.Name);
			}
		}
	}

	private async Task ApplyAsync(List<CalendarRange> ranges)
	{
		foreach (var range in ranges)
		{
			var previousStart = range.Calendar.StartDate.Date;
			var previousEnd = range.Calendar.EndDate.Date;
			range.Calendar.SetDateRange(range.StartDate, range.EndDate);
			var setting = await settings.FirstOrDefaultAsync(setting => setting.TenantId == CurrentTenant.Id &&
				setting.AcademicMediumId == range.Calendar.AcademicMediumId);
			if (setting != null)
			{
				if (range.StartDate < previousStart)
					await calendarManager.GenerateWeekendDaysAsync(range.Calendar, setting.WeekendRule, range.StartDate, previousStart.AddDays(-1));
				if (range.EndDate > previousEnd)
					await calendarManager.GenerateWeekendDaysAsync(range.Calendar, setting.WeekendRule, previousEnd.AddDays(1), range.EndDate);
			}
			await calendars.UpdateAsync(range.Calendar);
		}
	}

	private sealed record CalendarRange(AcademicCalendar Calendar, DateTime StartDate, DateTime EndDate);
}
