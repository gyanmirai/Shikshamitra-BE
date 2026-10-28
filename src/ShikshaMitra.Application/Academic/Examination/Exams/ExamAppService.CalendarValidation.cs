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
            await AsyncExecuter.ToListAsync((await calendarDayRepository.GetQueryableAsync())
                .Where(day => day.Status == CalendarDayStatus.Active && dates.Contains(day.Date.Date) &&
                    (day.AppliesToAll || (day.StandardId.HasValue && day.StandardId.Value == standardId) ||
                     (day.DivisionId.HasValue && divisions.Contains(day.DivisionId.Value))) &&
                    blockingTypes.Contains(day.DayType))
                .Select(day => new ExamCalendarBlock(day.Date, day.DayType, day.Title))));
        return async examDate =>
        {
            var date = examDate.Date;
            var blockingDay = (await blocks.Value).FirstOrDefault(day => day.Date.Date == date);
            if (blockingDay == null) return;
            var message = $"Cannot publish this exam because the scheduled date {date:dd MMM yyyy} conflicts with {blockingDay.DayType.ToString().ToLowerInvariant()} '{blockingDay.Title}' in the Academic Calendar.";
            throw new BusinessException("ShikshaMitra.Exam:ScheduleDateBlockedByCalendar", message)
                .WithData("ExamDate", date.ToString("yyyy-MM-dd"))
                .WithData("CalendarDay", blockingDay.Title)
                .WithData("DayType", blockingDay.DayType)
                .WithData("message", message);
        };
    }

    private sealed record ExamCalendarBlock(DateTime Date, DayType DayType, string Title);
}

