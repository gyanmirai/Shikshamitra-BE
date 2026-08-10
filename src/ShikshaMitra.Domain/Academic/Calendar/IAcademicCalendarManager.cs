using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.Calendar;

public interface IAcademicCalendarManager : IDomainService
{
    Task<AcademicCalendar> CreateAsync(
        Guid academicMediumId,
        Guid? academicYearId,
        string name,
        DateTime startDate,
        DateTime endDate,
        string? description = null,
        bool isDefault = false);

    Task GenerateWeekendDaysAsync(AcademicCalendar calendar, WeekendRule rule);

    Task ValidateDayRangeAsync(AcademicCalendar calendar, DateTime date);

    Task<List<CalendarImpactLog>> ScanImpactsAsync(AcademicCalendarDay day);
}
