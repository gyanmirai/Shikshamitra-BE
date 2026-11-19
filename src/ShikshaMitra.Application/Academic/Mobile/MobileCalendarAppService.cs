using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Calendar;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Mobile;

[Authorize]
[RemoteService(false)]
public class MobileCalendarAppService(MobileAcademicContextAppService context,
	IRepository<AcademicCalendar, Guid> calendars, IRepository<AcademicCalendarDay, Guid> days)
	: ApplicationService, IMobileCalendarAppService
{
	public async Task<List<AcademicCalendarDay>> ReadDaysAsync(MobilePlacement p, DateTime start, DateTime end)
	{
		var calendarIds = (await calendars.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.Status == CalendarStatus.Published &&
			(x.AcademicYearId == p.Year.Id || x.AcademicYearId == null) && (x.AcademicMediumId == p.MediumId || x.AcademicMediumId == null)))
			.Select(x => x.Id).ToArray();
		return await days.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(calendarIds, x.AcademicCalendarId) &&
			x.Status == CalendarDayStatus.Active && x.Date >= start && x.Date <= end &&
			(x.AppliesToAll || ((!x.StandardId.HasValue || x.StandardId == p.StandardId) && (!x.DivisionId.HasValue || x.DivisionId == p.DivisionId))));
	}

	public async Task<List<MobileSchoolMarkerDto>> GetAsync(MobileCalendarInput input)
	{
		var p = await context.ResolveAsync(input);
		if (input.Year is < 1900 or > 9998 || input.Month is < 1 or > 12) throw MobileAcademicContextAppService.Invalid("Supply a valid month.");
		var start = new DateTime(input.Year, input.Month, 1);
		var end = start.AddMonths(1).AddDays(-1);
		return (await ReadDaysAsync(p, start < p.Year.StartDate ? p.Year.StartDate : start, end > p.Year.EndDate ? p.Year.EndDate : end))
			.Where(x => x.DayType != DayType.WorkingDay).OrderBy(x => x.Date).ThenBy(x => x.Id).Select(Map).ToList();
	}

	public async Task<MobileSchoolMarkerDto> GetMarkerAsync(Guid id, MobileAcademicInput input)
	{
		var p = await context.ResolveAsync(input);
		var day = (await ReadDaysAsync(p, p.Year.StartDate, p.Year.EndDate)).SingleOrDefault(x => x.Id == id && x.DayType != DayType.WorkingDay)
			?? throw new EntityNotFoundException(typeof(AcademicCalendarDay), id);
		return Map(day);
	}

	private static MobileSchoolMarkerDto Map(AcademicCalendarDay d)
	{
		var closed = d.DayType is DayType.Holiday or DayType.Vacation or DayType.EmergencyClosure;
		// Calendar dates represent school wall time, regardless of the database DateTime kind.
		var schoolDate = DateTime.SpecifyKind(d.Date.Date, DateTimeKind.Unspecified);
		DateTimeOffset? At(TimeSpan? time) => time.HasValue ? new DateTimeOffset(schoolDate.Add(time.Value), TimeSpan.FromHours(5.5)) : null;
		return new(d.Id, closed ? "holiday" : "event", DateOnly.FromDateTime(d.Date), d.Title, d.Description,
			d.AppliesToAll ? "school" : "class", d.HolidayCategory?.ToString(), At(d.StartTime), At(d.EndTime), d.Venue, closed);
	}
}
