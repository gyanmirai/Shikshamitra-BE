using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Constants.Permissions;

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
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.View)]
    public async Task<PagedResultDto<AcademicCalendarDto>> GetCalendarsAsync(
        [FromQuery] Guid? academicMediumId,
        [FromQuery] CalendarStatus? status)
    {
        return await _calendarAppService.GetCalendarsAsync(academicMediumId, status);
    }

    [HttpGet("academic/calendars/{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.View)]
    public async Task<AcademicCalendarDto> GetCalendarByIdAsync([FromRoute] Guid id)
    {
        return await _calendarAppService.GetCalendarByIdAsync(id);
    }

    [HttpPost("academic/calendars")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.Add)]
    public async Task<AcademicCalendarDto> CreateCalendarAsync([FromBody] CreateCalendarDto input)
    {
        return await _calendarAppService.CreateCalendarAsync(input);
    }

    [HttpPut("academic/calendars/{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.Edit)]
    public async Task<AcademicCalendarDto> UpdateCalendarAsync([FromRoute] Guid id, [FromBody] UpdateCalendarDto input)
    {
        return await _calendarAppService.UpdateCalendarAsync(id, input);
    }

    [HttpDelete("academic/calendars/{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.Delete)]
    public async Task DeleteCalendarAsync([FromRoute] Guid id)
    {
        await _calendarAppService.DeleteCalendarAsync(id);
    }

    [HttpPost("academic/calendars/{id}/publish")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.Edit)]
    public async Task<AcademicCalendarDto> PublishCalendarAsync([FromRoute] Guid id)
    {
        return await _calendarAppService.PublishCalendarAsync(id);
    }

    [HttpPost("academic/calendars/{id}/archive")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.Edit)]
    public async Task<AcademicCalendarDto> ArchiveCalendarAsync([FromRoute] Guid id)
    {
        return await _calendarAppService.ArchiveCalendarAsync(id);
    }

    // ─── Calendar Days ──────────────────────────────────────────────────────

    [HttpGet("academic/calendars/{calendarId}/days")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.View)]
    public async Task<List<AcademicCalendarDayDto>> GetCalendarDaysAsync(
        [FromRoute] Guid calendarId, 
        [FromQuery] DayType? dayType, 
        [FromQuery] int? month, 
        [FromQuery] int? year)
    {
        return await _calendarAppService.GetCalendarDaysAsync(calendarId, dayType, month, year);
    }

    [HttpPost("academic/calendars/{calendarId}/days")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.Add)]
    public async Task<AcademicCalendarDayDto> CreateCalendarDayAsync(
        [FromRoute] Guid calendarId, 
        [FromBody] CreateCalendarDayDto input)
    {
        return await _calendarAppService.CreateCalendarDayAsync(calendarId, input);
    }

    [HttpPost("academic/calendars/{calendarId}/days/bulk")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.Add)]
    public async Task<List<AcademicCalendarDayDto>> CreateCalendarDaysBulkAsync(
        [FromRoute] Guid calendarId, 
        [FromBody] CreateCalendarDaysBulkDto input)
    {
        return await _calendarAppService.CreateCalendarDaysBulkAsync(calendarId, input);
    }

    [HttpPut("academic/calendar-days/{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.Edit)]
    public async Task<AcademicCalendarDayDto> UpdateCalendarDayAsync(
        [FromRoute] Guid id, 
        [FromBody] CreateCalendarDayDto input)
    {
        return await _calendarAppService.UpdateCalendarDayAsync(id, input);
    }

    [HttpDelete("academic/calendar-days/{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.Delete)]
    public async Task DeleteCalendarDayAsync([FromRoute] Guid id)
    {
        await _calendarAppService.DeleteCalendarDayAsync(id);
    }

    // ─── Working Hours ──────────────────────────────────────────────────────

    [HttpGet("academic/calendars/{calendarId}/working-hours")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_Settings.View)]
    public async Task<List<AcademicCalendarWorkingHourDto>> GetWorkingHoursAsync([FromRoute] Guid calendarId)
    {
        return await _calendarAppService.GetWorkingHoursAsync(calendarId);
    }

    [HttpPost("academic/calendars/{calendarId}/working-hours")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_Settings.Edit)]
    public async Task<AcademicCalendarWorkingHourDto> CreateWorkingHourAsync(
        [FromRoute] Guid calendarId, 
        [FromBody] AcademicCalendarWorkingHourDto input)
    {
        return await _calendarAppService.CreateWorkingHourAsync(calendarId, input);
    }

    [HttpPut("academic/calendar-working-hours/{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_Settings.Edit)]
    public async Task<AcademicCalendarWorkingHourDto> UpdateWorkingHourAsync(
        [FromRoute] Guid id, 
        [FromBody] AcademicCalendarWorkingHourDto input)
    {
        return await _calendarAppService.UpdateWorkingHourAsync(id, input);
    }

    [HttpDelete("academic/calendar-working-hours/{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_Settings.Edit)]
    public async Task DeleteWorkingHourAsync([FromRoute] Guid id)
    {
        await _calendarAppService.DeleteWorkingHourAsync(id);
    }

    // ─── Date Validation ────────────────────────────────────────────────────

    [HttpGet("academic/calendar/check-date")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_CalendarManagement.View)]
    public async Task<CheckDateResponseDto> CheckDateAsync([FromQuery] CheckDateQueryDto input)
    {
        return await _calendarAppService.CheckDateAsync(input);
    }

    // ─── Emergency Closures ──────────────────────────────────────────────────

    [HttpGet("academic/calendar-emergency-closures")]
    [HttpGet("academic/calendar-days/emergency-closures")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_EmergencyClosure.View)]
    public async Task<PagedResultDto<EmergencyClosureListItemDto>> GetEmergencyClosuresAsync(
        [FromQuery] Guid? academicMediumId,
        [FromQuery] CalendarDayStatus? status,
        [FromQuery] int skipCount = 0,
        [FromQuery] int maxResultCount = 10)
    {
        return await _calendarAppService.GetEmergencyClosuresAsync(academicMediumId, status, skipCount, maxResultCount);
    }

    [HttpPost("academic/calendar-days/emergency-closure")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_EmergencyClosure.Add)]
    public async Task<List<AcademicCalendarDayDto>> CreateEmergencyClosureAsync([FromBody] CreateEmergencyClosureDto input)
    {
        return await _calendarAppService.CreateEmergencyClosureAsync(input);
    }

    [HttpPost("academic/calendar-days/{id}/cancel-emergency-closure")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_EmergencyClosure.Delete)]
    public async Task CancelEmergencyClosureAsync([FromRoute] Guid id)
    {
        await _calendarAppService.CancelEmergencyClosureAsync(id);
    }

    [HttpPut("academic/calendar-days/{id}/emergency-closure")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_EmergencyClosure.Edit)]
    public async Task<AcademicCalendarDayDto> UpdateEmergencyClosureAsync([FromRoute] Guid id, [FromBody] UpdateEmergencyClosureDto input)
    {
        return await _calendarAppService.UpdateEmergencyClosureAsync(id, input);
    }

    // ─── Impact Logs ────────────────────────────────────────────────────────

    [HttpGet("academic/calendar-impact-logs")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_ImpactLogs.View)]
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
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_ImpactLogs.View)]
    public async Task<List<CalendarImpactLogDto>> GetImpactLogsByDayAsync([FromRoute] Guid calendarDayId)
    {
        return await _calendarAppService.GetImpactLogsByDayAsync(calendarDayId);
    }

    [HttpPost("academic/calendar-impact-logs/{id}/mark-resolved")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_ImpactLogs.Edit)]
    public async Task ResolveImpactLogAsync([FromRoute] Guid id, [FromBody] ResolveImpactLogDto input)
    {
        await _calendarAppService.ResolveImpactLogAsync(id, input);
    }

    [HttpPost("academic/calendar-impact-logs/{id}/ignore")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_ImpactLogs.Edit)]
    public async Task IgnoreImpactLogAsync([FromRoute] Guid id, [FromBody] IgnoreImpactLogDto input)
    {
        await _calendarAppService.IgnoreImpactLogAsync(id, input);
    }

    [HttpPost("academic/calendar-impact-logs/{id}/reschedule")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_ImpactLogs.Edit)]
    public async Task RescheduleImpactLogAsync([FromRoute] Guid id, [FromBody] RescheduleImpactLogDto input)
    {
        await _calendarAppService.RescheduleImpactLogAsync(id, input);
    }

    // ─── Audit Logs ─────────────────────────────────────────────────────────

    [HttpGet("academic/calendar-audit-logs")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_AuditLogs.View)]
    public async Task<PagedResultDto<AcademicCalendarAuditLogDto>> GetAuditLogsAsync(
        [FromQuery] Guid? calendarId, 
        [FromQuery] CalendarAuditAction? action,
        [FromQuery] int skipCount = 0,
        [FromQuery] int maxResultCount = 10)
    {
        return await _calendarAppService.GetAuditLogsAsync(calendarId, action, skipCount, maxResultCount);
    }

    [HttpGet("academic/calendar-audit-logs/by-calendar/{calendarId}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_AuditLogs.View)]
    public async Task<List<AcademicCalendarAuditLogDto>> GetAuditLogsByCalendarAsync([FromRoute] Guid calendarId)
    {
        return await _calendarAppService.GetAuditLogsByCalendarAsync(calendarId);
    }

    [HttpGet("academic/calendar-audit-logs/by-day/{calendarDayId}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_AuditLogs.View)]
    public async Task<List<AcademicCalendarAuditLogDto>> GetAuditLogsByDayAsync([FromRoute] Guid calendarDayId)
    {
        return await _calendarAppService.GetAuditLogsByDayAsync(calendarDayId);
    }

    [HttpGet("academic/calendar-audit-logs/summary")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_AuditLogs.View)]
    public async Task<AcademicCalendarAuditSummaryDto> GetAuditLogsSummaryAsync()
    {
        return await _calendarAppService.GetAuditLogsSummaryAsync();
    }

    // ─── Settings ───────────────────────────────────────────────────────────

    [HttpGet("academic/calendar/settings")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_Settings.View)]
    public async Task<AcademicCalendarSettingDto> GetCalendarSettingsAsync([FromQuery] Guid academicMediumId)
    {
        return await _calendarAppService.GetCalendarSettingsAsync(academicMediumId);
    }

    [HttpPost("academic/calendar/settings")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_Settings.Edit)]
    public async Task<AcademicCalendarSettingDto> CreateCalendarSettingsAsync([FromBody] AcademicCalendarSettingDto input)
    {
        return await _calendarAppService.CreateCalendarSettingsAsync(input);
    }

    [HttpPut("academic/calendar/settings/{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_Settings.Edit)]
    public async Task<AcademicCalendarSettingDto> UpdateCalendarSettingsAsync([FromRoute] Guid id, [FromBody] AcademicCalendarSettingDto input)
    {
        return await _calendarAppService.UpdateCalendarSettingsAsync(id, input);
    }

    // ─── Dashboard ──────────────────────────────────────────────────────────

    [HttpGet("academic/calendar/dashboard")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.AcademicCalendar_Dashboard.View)]
    public async Task<CalendarDashboardDto> GetCalendarDashboardAsync([FromQuery] Guid academicMediumId)
    {
        return await _calendarAppService.GetCalendarDashboardAsync(academicMediumId);
    }
}
