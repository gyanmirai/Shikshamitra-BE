using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using Volo.Abp;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public partial class AssignmentV2AppService
{
    private async Task<List<AssignmentCalendarDayRead>> ReadAssignmentDueDateCandidatesAsync(Guid academicYearId, IEnumerable<DateTime> dueDates)
    {
        var dates = dueDates.Select(date => date.Date).Distinct().ToArray();
        if (dates.Length == 0) return [];
        var calendars = await calendarRepository.GetQueryableAsync();
        var candidates = await asyncExecuter.ToListAsync(calendars.Where(calendar => calendar.AcademicYearId == academicYearId &&
                calendar.Status == CalendarStatus.Published)
            .Select(calendar => new { calendar.Id, calendar.IsDefault, calendar.PublishedAt }));
        var applicableCalendar = candidates.OrderByDescending(calendar => calendar.IsDefault)
            .ThenByDescending(calendar => calendar.PublishedAt ?? DateTime.MinValue).ThenBy(calendar => calendar.Id).FirstOrDefault();
        if (applicableCalendar == null) return [];
        var query = await calendarDayRepository.GetQueryableAsync();
        return await asyncExecuter.ToListAsync(query.Where(day => day.AcademicCalendarId == applicableCalendar.Id &&
                day.Status == CalendarDayStatus.Active && dates.Contains(day.Date.Date))
            .Select(day => new AssignmentCalendarDayRead(day.Date, day.DayType, day.Title, day.AppliesToAll, day.StandardId, day.DivisionId)));
    }

    private static void EnsureAssignmentDueDateAllowed(IEnumerable<AssignmentCalendarDayRead> days, Guid standardId, Guid divisionId, DateTime dueDate)
    {
        var date = dueDate.Date;
        var blockingDay = days.FirstOrDefault(day => day.Date.Date == date &&
            AssignmentCalendarPolicy.AppliesTo(day.AppliesToAll, day.StandardId, day.DivisionId, standardId, divisionId) &&
            CalendarDayBlockingRules.BlocksHomeworkDueDate(day.DayType));
        if (blockingDay == null) return;
        throw new BusinessException("ShikshaMitra.Assignment:DueDateBlockedByCalendar")
            .WithData("DueDate", date.ToString("yyyy-MM-dd"))
            .WithData("CalendarDay", blockingDay.Title)
            .WithData("DayType", blockingDay.DayType);
    }

    private sealed record AssignmentCalendarDayRead(DateTime Date, DayType DayType, string Title,
        bool AppliesToAll, Guid? StandardId, Guid? DivisionId);
}
