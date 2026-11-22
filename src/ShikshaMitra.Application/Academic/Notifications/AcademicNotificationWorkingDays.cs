using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicNotificationWorkingDays(ICurrentTenant tenant, IAsyncQueryableExecuter executer,
	IRepository<AcademicYear, Guid> years, IRepository<AcademicCalendar, Guid> calendars,
	IRepository<AcademicCalendarDay, Guid> calendarDays, ILectureSlotConfigRepository configs, ITimetableRepository timetables)
	: ITransientDependency
{
	public async Task<NotificationWorkingDays?> ReadAsync(NotificationRuleScope scope, DateOnly occurrenceDate,
		CancellationToken cancellationToken)
	{
		var first = DateOnly.FromDayNumber(Math.Max(0, occurrenceDate.DayNumber - NotificationReminderTimingPolicy.MaximumLookbackDays));
		return await ReadWindowAsync(scope, first, occurrenceDate, cancellationToken);
	}

	internal async Task<NotificationWorkingDays?> ReadWindowAsync(NotificationRuleScope scope, DateOnly windowStart, DateOnly windowEnd,
		CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (scope.AcademicYearId is not Guid yearId || scope.MediumId is not Guid mediumId || windowEnd == DateOnly.MaxValue)
			return null;
		var year = await executer.FirstOrDefaultAsync((await years.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.Id == yearId)
			.Select(x => new { x.StartDate, x.EndDate }), cancellationToken);
		if (year == null) return null;
		var first = DateOnly.FromDayNumber(Math.Max(DateOnly.FromDateTime(year.StartDate).DayNumber, windowStart.DayNumber));
		var last = DateOnly.FromDayNumber(Math.Min(DateOnly.FromDateTime(year.EndDate).DayNumber, windowEnd.DayNumber));
		if (last < first) return null;

		var start = first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
		var end = last.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
		var matching = (await calendars.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.AcademicYearId == yearId &&
			x.AcademicMediumId == mediumId && x.Status == CalendarStatus.Published && x.StartDate < end && x.EndDate >= start);
		var ranges = await executer.ToListAsync(matching.Select(x => new { x.StartDate, x.EndDate }), cancellationToken);
		var classId = scope.ClassId;
		var divisionId = scope.DivisionId;
		var applicableDays = (await calendarDays.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.Status == CalendarDayStatus.Active && x.Date >= start && x.Date < end &&
			(x.AppliesToAll || (classId.HasValue && x.StandardId == classId && (!x.DivisionId.HasValue || x.DivisionId == divisionId))));
		var days = await executer.ToListAsync(from day in applicableDays
			join calendar in matching on day.AcademicCalendarId equals calendar.Id
			where day.Date >= calendar.StartDate && day.Date <= calendar.EndDate
			select new { day.Date, day.DayType }, cancellationToken);
		var weekdays = await ReadWeekdaysAsync(tenantId, classId, divisionId, cancellationToken);
		return new(first, last, weekdays,
			ranges.Select(x => new NotificationCalendarRange(DateOnly.FromDateTime(x.StartDate), DateOnly.FromDateTime(x.EndDate))).ToArray(),
			days.Select(x => new NotificationCalendarDay(DateOnly.FromDateTime(x.Date), x.DayType)).Distinct().ToArray());
	}

	private async Task<IReadOnlyCollection<DayOfWeek>> ReadWeekdaysAsync(Guid tenantId, Guid? classId, Guid? divisionId, CancellationToken cancellationToken)
	{
		if (!classId.HasValue) return [];
		var groups = (await configs.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.StandardId == classId)
			.SelectMany(x => x.SlotGroups);
		var configured = await executer.ToListAsync(groups, cancellationToken);
		var indexes = configured.SelectMany(x => x.ActiveDays).Where(x => Enum.IsDefined((TimetableDayOfWeek)x)).ToHashSet();
		if (indexes.Count == 0 && divisionId.HasValue)
		{
			var slots = (await timetables.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.StandardId == classId &&
				x.DivisionId == divisionId && x.Status == TimetableStatus.Published).SelectMany(x => x.Slots.Select(slot => (int)slot.Day)).Distinct();
			indexes.UnionWith((await executer.ToListAsync(slots, cancellationToken)).Where(x => Enum.IsDefined((TimetableDayOfWeek)x)));
		}
		return indexes.Select(x => (DayOfWeek)(x + 1)).ToHashSet();
	}
}
