using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarManager : DomainService, IAcademicCalendarManager
{
    private readonly IRepository<AcademicCalendar, Guid> _calendarRepository;
    private readonly IRepository<AcademicCalendarDay, Guid> _calendarDayRepository;

    public AcademicCalendarManager(
        IRepository<AcademicCalendar, Guid> calendarRepository,
        IRepository<AcademicCalendarDay, Guid> calendarDayRepository)
    {
        _calendarRepository = calendarRepository;
        _calendarDayRepository = calendarDayRepository;
    }

    public async Task<AcademicCalendar> CreateAsync(
        Guid academicMediumId,
        Guid? academicYearId,
        string name,
        DateTime startDate,
        DateTime endDate,
        string? description = null,
        bool isDefault = false)
    {
        // Check if calendar already exists
        var existing = await _calendarRepository.FirstOrDefaultAsync(c =>
            c.AcademicMediumId == academicMediumId && c.Name == name);
        
        if (existing != null)
        {
            throw new UserFriendlyException($"An academic calendar named '{name}' already exists for this medium.");
        }

        // Handle default calendar adjustment
        if (isDefault)
        {
            var currentDefaults = await _calendarRepository.GetListAsync(c =>
                c.AcademicMediumId == academicMediumId && c.IsDefault);
            
            foreach (var def in currentDefaults)
            {
                def.UpdateInfo(def.Name, def.Description, false);
                await _calendarRepository.UpdateAsync(def);
            }
        }

        var calendarId = GuidGenerator.Create();
        return new AcademicCalendar(
            calendarId,
            CurrentTenant.Id,
            academicMediumId,
            academicYearId,
            name,
            startDate,
            endDate,
            description,
            isDefault
        );
    }

    public Task ValidateDayRangeAsync(AcademicCalendar calendar, DateTime date)
    {
        if (date.Date < calendar.StartDate.Date || date.Date > calendar.EndDate.Date)
        {
            throw new UserFriendlyException($"The date {date:yyyy-MM-dd} must fall within the calendar start date ({calendar.StartDate:yyyy-MM-dd}) and end date ({calendar.EndDate:yyyy-MM-dd}).");
        }
        return Task.CompletedTask;
    }

    public async Task GenerateWeekendDaysAsync(AcademicCalendar calendar, WeekendRule rule)
    {
        if (rule == WeekendRule.Custom)
        {
            return;
        }

        var weekendDays = new List<AcademicCalendarDay>();
        for (var date = calendar.StartDate.Date; date <= calendar.EndDate.Date; date = date.AddDays(1))
        {
            bool isWeekend = false;
            string title = "Weekend";

            if (rule == WeekendRule.SundayOnly && date.DayOfWeek == DayOfWeek.Sunday)
            {
                isWeekend = true;
                title = "Sunday Weekend";
            }
            else if (rule == WeekendRule.SaturdaySunday && 
                     (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday))
            {
                isWeekend = true;
                title = date.DayOfWeek == DayOfWeek.Saturday ? "Saturday Weekend" : "Sunday Weekend";
            }
            else if (rule == WeekendRule.AlternateSaturday)
            {
                if (date.DayOfWeek == DayOfWeek.Sunday)
                {
                    isWeekend = true;
                    title = "Sunday Weekend";
                }
                else if (date.DayOfWeek == DayOfWeek.Saturday)
                {
                    // Alternate Saturdays (2nd and 4th Saturday of the month)
                    var day = date.Day;
                    var occurrence = (day - 1) / 7 + 1;
                    if (occurrence == 2 || occurrence == 4)
                    {
                        isWeekend = true;
                        title = $"Alternate Saturday ({occurrence}nd/th Saturday)";
                    }
                }
            }

            if (isWeekend)
            {
                // Check if a day already exists on this date to prevent duplicate insertions
                var exists = calendar.Days != null && 
                             System.Linq.Enumerable.Any(calendar.Days, d => d.Date.Date == date.Date);
                
                if (!exists)
                {
                    var dayId = GuidGenerator.Create();
                    var day = new AcademicCalendarDay(
                        dayId,
                        CurrentTenant.Id,
                        calendar.Id,
                        date,
                        DayType.Holiday,
                        title,
                        description: "System generated weekend holiday",
                        appliesToAll: true
                    );
                    calendar.Days.Add(day);
                }
            }
        }
        await Task.CompletedTask;
    }

    public async Task<List<CalendarImpactLog>> ScanImpactsAsync(AcademicCalendarDay day)
    {
        var impacts = new List<CalendarImpactLog>();

        // Under standard execution, this would run Repository scans over exams, homework, and timetables.
        // We simulate this by returning structured alerts if the day blocks key functions.
        if (CalendarDayBlockingRules.BlocksTimetable(day.DayType) && day.Status == CalendarDayStatus.Active)
        {
            impacts.Add(new CalendarImpactLog(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                day.Id,
                CalendarImpactType.TimetableSlot,
                $"Timetable lectures on {day.Date:yyyy-MM-dd}",
                day.Date,
                $"timetable schedule blocked due to active day type: {day.DayType}"
            ));
        }

        if (CalendarDayBlockingRules.BlocksHomeworkDueDate(day.DayType) && day.Status == CalendarDayStatus.Active)
        {
            impacts.Add(new CalendarImpactLog(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                day.Id,
                CalendarImpactType.HomeworkDueDate,
                $"Homework assignments due on {day.Date:yyyy-MM-dd}",
                day.Date,
                $"Homework due dates blocked due to active day type: {day.DayType}"
            ));
        }

        if (CalendarDayBlockingRules.BlocksExamSchedule(day.DayType) && day.Status == CalendarDayStatus.Active)
        {
            impacts.Add(new CalendarImpactLog(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                day.Id,
                CalendarImpactType.ExamSchedule,
                $"Exam schedule overlapping on {day.Date:yyyy-MM-dd}",
                day.Date,
                $"Exams scheduled on this date fall within holiday/vacation window."
            ));
        }

        await Task.CompletedTask;
        return impacts;
    }
}
