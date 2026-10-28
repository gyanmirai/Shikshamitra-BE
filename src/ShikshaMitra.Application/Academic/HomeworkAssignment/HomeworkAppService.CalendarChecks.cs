using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using Volo.Abp;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public partial class HomeworkAppService
{
    private async Task<List<HomeworkCalendarDayRead>> ReadHomeworkDueDateCandidatesAsync(Guid standardId,
        IEnumerable<(Guid DivisionId, DateTime DueDate)> schedules)
    {
        var rows = schedules.ToList();
        if (rows.Count == 0) return [];
        var dates = rows.Select(row => row.DueDate.Date).Distinct().ToArray();
        var divisions = rows.Select(row => row.DivisionId).Distinct().ToArray();
        var query = await calendarDayRepository.GetQueryableAsync();
        return await asyncExecuter.ToListAsync(query.Where(day => day.Status == CalendarDayStatus.Active && dates.Contains(day.Date.Date) &&
                (day.AppliesToAll || day.StandardId == standardId || (day.DivisionId.HasValue && divisions.Contains(day.DivisionId.Value))))
            .Select(day => new HomeworkCalendarDayRead(day.Date, day.DayType, day.Title, day.AppliesToAll, day.StandardId, day.DivisionId)));
    }

    private static void EnsureHomeworkDueDateAllowed(IEnumerable<HomeworkCalendarDayRead> days, Guid standardId,
        Guid divisionId, DateTime dueDate, string? divisionName = null)
    {
        var date = dueDate.Date;
        var blockingDay = days.FirstOrDefault(day => day.Date.Date == date &&
            (day.AppliesToAll || day.StandardId == standardId || day.DivisionId == divisionId) &&
            CalendarDayBlockingRules.BlocksHomeworkDueDate(day.DayType));
        if (blockingDay == null) return;
        var targetDivision = string.IsNullOrWhiteSpace(divisionName) ? "the selected division" : divisionName;
        throw new UserFriendlyException(
            $"Due Date {date:dd/MM/yyyy} for {targetDivision} is blocked by the academic calendar entry \"{blockingDay.Title}\". Choose another due date.");
    }

    private sealed record HomeworkCalendarDayRead(DateTime Date, DayType DayType, string Title,
        bool AppliesToAll, Guid? StandardId, Guid? DivisionId);
}
