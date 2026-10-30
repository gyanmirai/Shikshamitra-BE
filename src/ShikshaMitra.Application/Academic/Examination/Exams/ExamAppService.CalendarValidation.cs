using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Examination.Exams;

public partial class ExamAppService
{
    private Func<DateTime, Task> CreateExamCalendarValidator(Guid standardId, IEnumerable<Guid> divisionIds, IEnumerable<DateTime?> scheduledDates)
    {
        var divisions = divisionIds.Distinct().ToList();
        var dates = scheduledDates.Where(date => date.HasValue)
            .Select(date => DateTime.SpecifyKind(date!.Value.Date, DateTimeKind.Utc)).Distinct().ToList();
        var blockingTypes = Enum.GetValues<DayType>().Where(CalendarDayBlockingRules.BlocksExamSchedule).ToList();
        var blocks = new Lazy<Task<List<ExamCalendarBlock>>>(async () =>
        {
            if (dates.Count == 0) return [];
            var mediumId = await GetAcademicMediumIdForStandardAsync(standardId);
            var firstDate = dates.Min();
            var lastDate = dates.Max();
            var calendars = await calendarRepository.GetListAsync(calendar =>
                calendar.AcademicMediumId == mediumId && calendar.Status != CalendarStatus.Archived &&
                calendar.StartDate.Date <= lastDate && calendar.EndDate.Date >= firstDate);
            var calendarByDate = dates.ToDictionary(date => date, date => SelectExamCalendar(calendars, date)?.Id);
            var calendarIds = calendarByDate.Values.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            if (calendarIds.Count == 0) return [];
            var days = await AsyncExecuter.ToListAsync((await calendarDayRepository.GetQueryableAsync())
                .Where(day => calendarIds.Contains(day.AcademicCalendarId) &&
                    day.Status == CalendarDayStatus.Active && dates.Contains(day.Date.Date) &&
                    (day.AppliesToAll ||
                     ((!day.StandardId.HasValue || day.StandardId.Value == standardId) &&
                      (day.StandardId.HasValue || day.DivisionId.HasValue) &&
                      (!day.DivisionId.HasValue || divisions.Contains(day.DivisionId.Value)))) &&
                    blockingTypes.Contains(day.DayType))
                .OrderBy(day => day.Id)
                .Select(day => new ExamCalendarBlock(day.Id, day.AcademicCalendarId, day.Date, day.DayType, day.Title)));
            return days.Where(day => calendarByDate.GetValueOrDefault(day.Date.Date) == day.CalendarId).ToList();
        });
        return async examDate =>
        {
            var date = examDate.Date;
            var blockingDay = (await blocks.Value).FirstOrDefault(day => day.Date.Date == date);
            if (blockingDay == null) return;
            var message = $"Cannot publish this exam because the scheduled date {date:dd MMM yyyy} conflicts with {blockingDay.DayType.ToString().ToLowerInvariant()} '{blockingDay.Title}' in the Academic Calendar.";
            throw new BusinessException("ShikshaMitra.Exam:ScheduleDateBlockedByCalendar", message)
                .WithData("ExamDate", date.ToString("yyyy-MM-dd"))
                .WithData("CalendarDay", blockingDay.Title)
                .WithData("AcademicCalendarId", blockingDay.CalendarId)
                .WithData("AcademicCalendarDayId", blockingDay.Id)
                .WithData("CalendarDate", blockingDay.Date.ToString("yyyy-MM-dd"))
                .WithData("DayType", blockingDay.DayType)
                .WithData("message", message);
        };
    }

    private static AcademicCalendar? SelectExamCalendar(IEnumerable<AcademicCalendar> calendars, DateTime date) =>
        calendars.Where(calendar => calendar.Status != CalendarStatus.Archived &&
                calendar.StartDate.Date <= date.Date && calendar.EndDate.Date >= date.Date)
            .OrderByDescending(calendar => calendar.Status == CalendarStatus.Published ? 2 : calendar.IsDefault ? 1 : 0)
            .ThenByDescending(calendar => calendar.CreationTime)
            .ThenByDescending(calendar => calendar.Id)
            .FirstOrDefault();

    private sealed record ExamCalendarBlock(Guid Id, Guid CalendarId, DateTime Date, DayType DayType, string Title);
}

