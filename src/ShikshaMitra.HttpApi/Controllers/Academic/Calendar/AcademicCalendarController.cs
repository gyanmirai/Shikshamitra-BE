using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Academic.Calendar;

namespace ShikshaMitra.Controllers.Academic.Calendar;

[Route("api")]
[Authorize]
[ApiController]
public class AcademicCalendarController : AbpController
{
    private readonly IAcademicCalendarAppService _calendarAppService;

    public AcademicCalendarController(IAcademicCalendarAppService calendarAppService)
    {
        _calendarAppService = calendarAppService;
    }

    // ─── Calendar Setup ─────────────────────────────────────────────────────

    [HttpGet("academic/calendars")]
    public async Task<PagedResultDto<AcademicCalendarDto>> GetCalendarsAsync(
        [FromQuery] Guid? academicYearId, 
        [FromQuery] CalendarStatus? status)
    {
        return await _calendarAppService.GetCalendarsAsync(academicYearId, status);
    }

    [HttpGet("academic/calendars/{id}")]
    public async Task<AcademicCalendarDto> GetCalendarByIdAsync([FromRoute] Guid id)
    {
        return await _calendarAppService.GetCalendarByIdAsync(id);
    }

    [HttpPost("academic/calendars")]
    public async Task<AcademicCalendarDto> CreateCalendarAsync([FromBody] CreateCalendarDto input)
    {
        return await _calendarAppService.CreateCalendarAsync(input);
    }

    [HttpPut("academic/calendars/{id}")]
    public async Task<AcademicCalendarDto> UpdateCalendarAsync([FromRoute] Guid id, [FromBody] UpdateCalendarDto input)
    {
        return await _calendarAppService.UpdateCalendarAsync(id, input);
    }

    [HttpDelete("academic/calendars/{id}")]
    public async Task DeleteCalendarAsync([FromRoute] Guid id)
    {
        await _calendarAppService.DeleteCalendarAsync(id);
    }

    [HttpPost("academic/calendars/{id}/publish")]
    public async Task<AcademicCalendarDto> PublishCalendarAsync([FromRoute] Guid id)
    {
        return await _calendarAppService.PublishCalendarAsync(id);
    }

    [HttpPost("academic/calendars/{id}/archive")]
    public async Task<AcademicCalendarDto> ArchiveCalendarAsync([FromRoute] Guid id)
    {
        return await _calendarAppService.ArchiveCalendarAsync(id);
    }

    // ─── Calendar Days ──────────────────────────────────────────────────────

    [HttpGet("academic/calendars/{calendarId}/days")]
    public async Task<List<AcademicCalendarDayDto>> GetCalendarDaysAsync(
        [FromRoute] Guid calendarId, 
        [FromQuery] DayType? dayType, 
        [FromQuery] int? month, 
        [FromQuery] int? year)
    {
        return await _calendarAppService.GetCalendarDaysAsync(calendarId, dayType, month, year);
    }

    [HttpPost("academic/calendars/{calendarId}/days")]
    public async Task<AcademicCalendarDayDto> CreateCalendarDayAsync(
        [FromRoute] Guid calendarId, 
        [FromBody] CreateCalendarDayDto input)
    {
        return await _calendarAppService.CreateCalendarDayAsync(calendarId, input);
    }

    [HttpPost("academic/calendars/{calendarId}/days/bulk")]
    public async Task<List<AcademicCalendarDayDto>> CreateCalendarDaysBulkAsync(
        [FromRoute] Guid calendarId, 
        [FromBody] CreateCalendarDaysBulkDto input)
    {
        return await _calendarAppService.CreateCalendarDaysBulkAsync(calendarId, input);
    }

    [HttpPut("academic/calendar-days/{id}")]
    public async Task<AcademicCalendarDayDto> UpdateCalendarDayAsync(
        [FromRoute] Guid id, 
        [FromBody] CreateCalendarDayDto input)
    {
        return await _calendarAppService.UpdateCalendarDayAsync(id, input);
    }

    [HttpDelete("academic/calendar-days/{id}")]
    public async Task DeleteCalendarDayAsync([FromRoute] Guid id)
    {
        await _calendarAppService.DeleteCalendarDayAsync(id);
    }

    // ─── Working Hours ──────────────────────────────────────────────────────

    [HttpGet("academic/calendars/{calendarId}/working-hours")]
    public async Task<List<AcademicCalendarWorkingHourDto>> GetWorkingHoursAsync([FromRoute] Guid calendarId)
    {
        return await _calendarAppService.GetWorkingHoursAsync(calendarId);
    }

    [HttpPost("academic/calendars/{calendarId}/working-hours")]
    public async Task<AcademicCalendarWorkingHourDto> CreateWorkingHourAsync(
        [FromRoute] Guid calendarId, 
        [FromBody] AcademicCalendarWorkingHourDto input)
    {
        return await _calendarAppService.CreateWorkingHourAsync(calendarId, input);
    }

    [HttpPut("academic/calendar-working-hours/{id}")]
    public async Task<AcademicCalendarWorkingHourDto> UpdateWorkingHourAsync(
        [FromRoute] Guid id, 
        [FromBody] AcademicCalendarWorkingHourDto input)
    {
        return await _calendarAppService.UpdateWorkingHourAsync(id, input);
    }

    [HttpDelete("academic/calendar-working-hours/{id}")]
    public async Task DeleteWorkingHourAsync([FromRoute] Guid id)
    {
        await _calendarAppService.DeleteWorkingHourAsync(id);
    }

    // ─── Date Validation ────────────────────────────────────────────────────

    [HttpGet("academic/calendar/check-date")]
    public async Task<CheckDateResponseDto> CheckDateAsync([FromQuery] CheckDateQueryDto input)
    {
        return await _calendarAppService.CheckDateAsync(input);
    }

    // ─── Emergency Closures ──────────────────────────────────────────────────

    [HttpGet("academic/calendar-emergency-closures")]
    [HttpGet("academic/calendar-days/emergency-closures")]
    public async Task<PagedResultDto<EmergencyClosureListItemDto>> GetEmergencyClosuresAsync(
        [FromQuery] Guid? academicYearId,
        [FromQuery] CalendarDayStatus? status,
        [FromQuery] int skipCount = 0,
        [FromQuery] int maxResultCount = 10)
    {
        return await _calendarAppService.GetEmergencyClosuresAsync(academicYearId, status, skipCount, maxResultCount);
    }

    [HttpPost("academic/calendar-days/emergency-closure")]
    public async Task<List<AcademicCalendarDayDto>> CreateEmergencyClosureAsync([FromBody] CreateEmergencyClosureDto input)
    {
        return await _calendarAppService.CreateEmergencyClosureAsync(input);
    }

    [HttpPost("academic/calendar-days/{id}/cancel-emergency-closure")]
    public async Task CancelEmergencyClosureAsync([FromRoute] Guid id)
    {
        await _calendarAppService.CancelEmergencyClosureAsync(id);
    }

    // ─── Impact Logs ────────────────────────────────────────────────────────

    [HttpGet("academic/calendar-impact-logs")]
    public async Task<PagedResultDto<CalendarImpactLogDto>> GetImpactLogsAsync(
        [FromQuery] Guid? calendarDayId, 
        [FromQuery] CalendarImpactType? impactType, 
        [FromQuery] CalendarImpactStatus? status,
        [FromQuery] int skipCount = 0,
        [FromQuery] int maxResultCount = 10)
    {
        return await _calendarAppService.GetImpactLogsAsync(calendarDayId, impactType, status, skipCount, maxResultCount);
    }

    [HttpGet("academic/calendar-impact-logs/by-day/{calendarDayId}")]
    public async Task<List<CalendarImpactLogDto>> GetImpactLogsByDayAsync([FromRoute] Guid calendarDayId)
    {
        return await _calendarAppService.GetImpactLogsByDayAsync(calendarDayId);
    }

    [HttpPost("academic/calendar-impact-logs/{id}/mark-resolved")]
    public async Task ResolveImpactLogAsync([FromRoute] Guid id, [FromBody] ResolveImpactLogDto input)
    {
        await _calendarAppService.ResolveImpactLogAsync(id, input);
    }

    [HttpPost("academic/calendar-impact-logs/{id}/ignore")]
    public async Task IgnoreImpactLogAsync([FromRoute] Guid id, [FromBody] IgnoreImpactLogDto input)
    {
        await _calendarAppService.IgnoreImpactLogAsync(id, input);
    }

    [HttpPost("academic/calendar-impact-logs/{id}/reschedule")]
    public async Task RescheduleImpactLogAsync([FromRoute] Guid id, [FromBody] RescheduleImpactLogDto input)
    {
        await _calendarAppService.RescheduleImpactLogAsync(id, input);
    }

    // ─── Audit Logs ─────────────────────────────────────────────────────────

    [HttpGet("academic/calendar-audit-logs")]
    public async Task<PagedResultDto<AcademicCalendarAuditLogDto>> GetAuditLogsAsync(
        [FromQuery] Guid? calendarId, 
        [FromQuery] CalendarAuditAction? action,
        [FromQuery] int skipCount = 0,
        [FromQuery] int maxResultCount = 10)
    {
        return await _calendarAppService.GetAuditLogsAsync(calendarId, action, skipCount, maxResultCount);
    }

    [HttpGet("academic/calendar-audit-logs/by-calendar/{calendarId}")]
    public async Task<List<AcademicCalendarAuditLogDto>> GetAuditLogsByCalendarAsync([FromRoute] Guid calendarId)
    {
        return await _calendarAppService.GetAuditLogsByCalendarAsync(calendarId);
    }

    [HttpGet("academic/calendar-audit-logs/by-day/{calendarDayId}")]
    public async Task<List<AcademicCalendarAuditLogDto>> GetAuditLogsByDayAsync([FromRoute] Guid calendarDayId)
    {
        return await _calendarAppService.GetAuditLogsByDayAsync(calendarDayId);
    }

    [HttpGet("academic/calendar-audit-logs/summary")]
    public async Task<AcademicCalendarAuditSummaryDto> GetAuditLogsSummaryAsync()
    {
        return await _calendarAppService.GetAuditLogsSummaryAsync();
    }

    // ─── Settings ───────────────────────────────────────────────────────────

    [HttpGet("academic/calendar/settings")]
    public async Task<AcademicCalendarSettingDto> GetCalendarSettingsAsync([FromQuery] Guid academicYearId)
    {
        return await _calendarAppService.GetCalendarSettingsAsync(academicYearId);
    }

    [HttpPost("academic/calendar/settings")]
    public async Task<AcademicCalendarSettingDto> CreateCalendarSettingsAsync([FromBody] AcademicCalendarSettingDto input)
    {
        return await _calendarAppService.CreateCalendarSettingsAsync(input);
    }

    [HttpPut("academic/calendar/settings/{id}")]
    public async Task<AcademicCalendarSettingDto> UpdateCalendarSettingsAsync([FromRoute] Guid id, [FromBody] AcademicCalendarSettingDto input)
    {
        return await _calendarAppService.UpdateCalendarSettingsAsync(id, input);
    }

    // ─── Dashboard ──────────────────────────────────────────────────────────

    [HttpGet("academic/calendar/dashboard")]
    public async Task<CalendarDashboardDto> GetCalendarDashboardAsync([FromQuery] Guid academicYearId)
    {
        return await _calendarAppService.GetCalendarDashboardAsync(academicYearId);
    }
}
