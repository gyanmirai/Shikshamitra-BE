using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Calendar;

public interface IAcademicCalendarAppService : IApplicationService
{
    // Calendar Setup
    Task<PagedResultDto<AcademicCalendarDto>> GetCalendarsAsync(Guid? academicMediumId, CalendarStatus? status);
    Task<AcademicCalendarDto> GetCalendarByIdAsync(Guid id);
    Task<AcademicCalendarDto> CreateCalendarAsync(CreateCalendarDto input);
    Task<AcademicCalendarDto> UpdateCalendarAsync(Guid id, UpdateCalendarDto input);
    Task DeleteCalendarAsync(Guid id);
    Task<AcademicCalendarDto> PublishCalendarAsync(Guid id);
    Task<AcademicCalendarDto> ArchiveCalendarAsync(Guid id);

    // Calendar Days
    Task<List<AcademicCalendarDayDto>> GetCalendarDaysAsync(Guid calendarId, DayType? dayType, int? month, int? year);
    Task<AcademicCalendarDayDto> CreateCalendarDayAsync(Guid calendarId, CreateCalendarDayDto input);
    Task<List<AcademicCalendarDayDto>> CreateCalendarDaysBulkAsync(Guid calendarId, CreateCalendarDaysBulkDto input);
    Task<AcademicCalendarDayDto> UpdateCalendarDayAsync(Guid id, CreateCalendarDayDto input);
    Task DeleteCalendarDayAsync(Guid id);

    // Working Hours
    Task<List<AcademicCalendarWorkingHourDto>> GetWorkingHoursAsync(Guid calendarId);
    Task<AcademicCalendarWorkingHourDto> CreateWorkingHourAsync(Guid calendarId, AcademicCalendarWorkingHourDto input);
    Task<AcademicCalendarWorkingHourDto> UpdateWorkingHourAsync(Guid id, AcademicCalendarWorkingHourDto input);
    Task DeleteWorkingHourAsync(Guid id);

    // Date Validation
    Task<CheckDateResponseDto> CheckDateAsync(CheckDateQueryDto input);

    // Emergency Closures
    Task<PagedResultDto<EmergencyClosureListItemDto>> GetEmergencyClosuresAsync(Guid? academicMediumId, CalendarDayStatus? status, int skipCount = 0, int maxResultCount = 10);
    Task<List<AcademicCalendarDayDto>> CreateEmergencyClosureAsync(CreateEmergencyClosureDto input);
    Task<AcademicCalendarDayDto> UpdateEmergencyClosureAsync(Guid dayId, UpdateEmergencyClosureDto input);
    Task CancelEmergencyClosureAsync(Guid dayId);

    // Impact Logs
    Task<PagedResultDto<CalendarImpactLogDto>> GetImpactLogsAsync(Guid? calendarDayId, CalendarImpactType? impactType, CalendarImpactStatus? status, int skipCount = 0, int maxResultCount = 10);
    Task<List<CalendarImpactLogDto>> GetImpactLogsByDayAsync(Guid calendarDayId);
    Task ResolveImpactLogAsync(Guid id, ResolveImpactLogDto input);
    Task IgnoreImpactLogAsync(Guid id, IgnoreImpactLogDto input);
    Task RescheduleImpactLogAsync(Guid id, RescheduleImpactLogDto input);

    // Audit Logs
    Task<PagedResultDto<AcademicCalendarAuditLogDto>> GetAuditLogsAsync(Guid? calendarId, CalendarAuditAction? action, int skipCount = 0, int maxResultCount = 10);
    Task<List<AcademicCalendarAuditLogDto>> GetAuditLogsByCalendarAsync(Guid calendarId);
    Task<List<AcademicCalendarAuditLogDto>> GetAuditLogsByDayAsync(Guid calendarDayId);
    Task<AcademicCalendarAuditSummaryDto> GetAuditLogsSummaryAsync();

    // Settings
    Task<AcademicCalendarSettingDto> GetCalendarSettingsAsync(Guid academicMediumId);
    Task<AcademicCalendarSettingDto> CreateCalendarSettingsAsync(AcademicCalendarSettingDto input);
    Task<AcademicCalendarSettingDto> UpdateCalendarSettingsAsync(Guid id, AcademicCalendarSettingDto input);

    // Dashboard
    Task<CalendarDashboardDto> GetCalendarDashboardAsync(Guid academicMediumId);
}
