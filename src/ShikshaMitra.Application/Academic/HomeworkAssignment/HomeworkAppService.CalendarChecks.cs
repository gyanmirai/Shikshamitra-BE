using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using Volo.Abp;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public partial class HomeworkAppService
{
    private async Task<List<HomeworkCalendarDayRead>> ReadHomeworkDueDateCandidatesAsync(Guid academicMediumId, Guid standardId,
        IEnumerable<(Guid DivisionId, DateTime DueDate)> schedules)
    {
        var rows = schedules.ToList();
        if (rows.Count == 0) return [];
        var dates = rows.Select(row => LearningTaskScheduleTimestamp.ToSchoolDate(row.DueDate)).Distinct().ToArray();
        var divisions = rows.Select(row => row.DivisionId).Distinct().ToArray();
        var firstDate = dates.Min();
        var lastDate = dates.Max();
        var calendars = await asyncExecuter.ToListAsync((await calendarRepository.GetQueryableAsync())
            .Where(calendar => calendar.AcademicMediumId == academicMediumId && calendar.Status != CalendarStatus.Archived &&
                calendar.StartDate.Date <= lastDate && calendar.EndDate.Date >= firstDate)
            .Select(calendar => new { calendar.Id, calendar.StartDate, calendar.EndDate, calendar.Status, calendar.IsDefault, calendar.CreationTime }));
        var calendarByDate = dates.ToDictionary(date => date, date => calendars
            .Where(calendar => calendar.StartDate.Date <= date && calendar.EndDate.Date >= date)
            .OrderByDescending(calendar => calendar.Status == CalendarStatus.Published ? 2 : calendar.IsDefault ? 1 : 0)
            .ThenByDescending(calendar => calendar.CreationTime).ThenByDescending(calendar => calendar.Id)
            .FirstOrDefault()?.Id);
        var calendarIds = calendarByDate.Values.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (calendarIds.Length == 0) return [];
        var query = await calendarDayRepository.GetQueryableAsync();
        var days = await asyncExecuter.ToListAsync(query.Where(day => Enumerable.Contains(calendarIds, day.AcademicCalendarId) &&
                day.Status == CalendarDayStatus.Active && Enumerable.Contains(dates, day.Date.Date) &&
                (day.AppliesToAll || day.StandardId == standardId || (day.DivisionId.HasValue && Enumerable.Contains(divisions, day.DivisionId.Value))))
            .OrderBy(day => day.Id)
            .Select(day => new HomeworkCalendarDayRead(day.AcademicCalendarId, day.Date, day.DayType, day.Title, day.AppliesToAll, day.StandardId, day.DivisionId)));
        return days.Where(day => calendarByDate.GetValueOrDefault(day.Date.Date) == day.CalendarId).ToList();
    }

    private static void EnsureHomeworkDueDateAllowed(IEnumerable<HomeworkCalendarDayRead> days, Guid standardId,
        Guid divisionId, DateTime dueDate, string? divisionName = null)
    {
        var date = LearningTaskScheduleTimestamp.ToSchoolDate(dueDate);
        var blockingDay = days.FirstOrDefault(day => day.Date.Date == date &&
            (day.AppliesToAll || ((!day.StandardId.HasValue || day.StandardId == standardId) &&
                (!day.DivisionId.HasValue || day.DivisionId == divisionId) &&
                (day.StandardId.HasValue || day.DivisionId.HasValue))) &&
            CalendarDayBlockingRules.BlocksHomeworkDueDate(day.DayType));
        if (blockingDay == null) return;
        var targetDivision = string.IsNullOrWhiteSpace(divisionName) ? "the selected division" : divisionName;
        throw new UserFriendlyException(
            $"Due Date {date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} for {targetDivision} is blocked by the academic calendar entry \"{blockingDay.Title}\". Choose another due date.");
    }

    private sealed record HomeworkCalendarDayRead(Guid CalendarId, DateTime Date, DayType DayType, string Title,
        bool AppliesToAll, Guid? StandardId, Guid? DivisionId);
}
