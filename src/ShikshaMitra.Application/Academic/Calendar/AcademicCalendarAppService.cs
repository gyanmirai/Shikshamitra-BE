using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using EducationMedium = ShikshaMitra.TenantSettings.EducationMediums.EducationMedium;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Academic.Calendar;

[Authorize]
public class AcademicCalendarAppService : ApplicationService, IAcademicCalendarAppService
{
    private readonly IRepository<AcademicCalendar, Guid> _calendarRepository;
    private readonly IRepository<AcademicCalendarDay, Guid> _calendarDayRepository;
    private readonly IRepository<AcademicCalendarWorkingHour, Guid> _workingHourRepository;
    private readonly IRepository<AcademicCalendarSetting, Guid> _settingRepository;
    private readonly IRepository<CalendarImpactLog, Guid> _impactLogRepository;
    private readonly IRepository<AcademicCalendarAuditLog, Guid> _auditLogRepository;
    private readonly IRepository<AcademicCalendarAuditReviewNote, Guid> _auditReviewNoteRepository;
    private readonly IAcademicYearRepository _academicYearRepository;
    private readonly IRepository<EducationMedium, Guid> _academicMediumRepository;
    private readonly IAcademicCalendarManager _calendarManager;
    private readonly AcademicCalendarAuditWriter _auditWriter;
    private readonly RoomConflictService _roomConflictService;
    private readonly IRepository<ExamType, Guid> _examTypeRepository;
    private readonly IRepository<Exam, Guid> _examRepository;
    private readonly IRepository<Standard, Guid> _standardRepository;
    private readonly IRepository<EducationMediumStage, Guid> _educationMediumStageRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;

    public AcademicCalendarAppService(
        IRepository<AcademicCalendar, Guid> calendarRepository,
        IRepository<AcademicCalendarDay, Guid> calendarDayRepository,
        IRepository<AcademicCalendarWorkingHour, Guid> workingHourRepository,
        IRepository<AcademicCalendarSetting, Guid> settingRepository,
        IRepository<CalendarImpactLog, Guid> impactLogRepository,
        IRepository<AcademicCalendarAuditLog, Guid> auditLogRepository,
        IRepository<AcademicCalendarAuditReviewNote, Guid> auditReviewNoteRepository,
        IAcademicYearRepository academicYearRepository,
        IRepository<EducationMedium, Guid> academicMediumRepository,
        IAcademicCalendarManager calendarManager,
        AcademicCalendarAuditWriter auditWriter,
        RoomConflictService roomConflictService,
        IRepository<ExamType, Guid> examTypeRepository,
        IRepository<Exam, Guid> examRepository,
        IRepository<Standard, Guid> standardRepository,
        IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
        IRepository<IdentityUser, Guid> identityUserRepository)
    {
        _calendarRepository = calendarRepository;
        _calendarDayRepository = calendarDayRepository;
        _workingHourRepository = workingHourRepository;
        _settingRepository = settingRepository;
        _impactLogRepository = impactLogRepository;
        _auditLogRepository = auditLogRepository;
        _auditReviewNoteRepository = auditReviewNoteRepository;
        _academicYearRepository = academicYearRepository;
        _academicMediumRepository = academicMediumRepository;
        _calendarManager = calendarManager;
        _auditWriter = auditWriter;
        _roomConflictService = roomConflictService;
        _examTypeRepository = examTypeRepository;
        _examRepository = examRepository;
        _standardRepository = standardRepository;
        _educationMediumStageRepository = educationMediumStageRepository;
        _identityUserRepository = identityUserRepository;
    }

    // ─── Calendar Setup CRUD ────────────────────────────────────────────────

    public async Task<PagedResultDto<AcademicCalendarDto>> GetCalendarsAsync(Guid? academicMediumId, CalendarStatus? status)
    {
        var query = await _calendarRepository.GetQueryableAsync();
        
        if (academicMediumId.HasValue)
        {
            query = query.Where(c => c.AcademicMediumId == academicMediumId.Value);
        }
        
        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(c => c.CreationTime).ToList();
        
        var dtos = ObjectMapper.Map<List<AcademicCalendar>, List<AcademicCalendarDto>>(items);
        
        // Populate stats for each calendar DTO
        foreach (var dto in dtos)
        {
            var days = await _calendarDayRepository.GetListAsync(d => d.AcademicCalendarId == dto.Id);
            dto.TotalWorkingDays = days.Count(d => d.DayType == DayType.WorkingDay && d.Status == CalendarDayStatus.Active);
            dto.TotalHolidays = days.Count(d => d.DayType == DayType.Holiday && d.Status == CalendarDayStatus.Active);
            dto.TotalVacationDays = days.Count(d => d.DayType == DayType.Vacation && d.Status == CalendarDayStatus.Active);
            dto.TotalHalfDays = days.Count(d => d.DayType == DayType.HalfDay && d.Status == CalendarDayStatus.Active);
            dto.TotalEvents = days.Count(d => d.DayType == DayType.Event && d.Status == CalendarDayStatus.Active);
            dto.TotalExamDays = days.Count(d => d.DayType == DayType.Exam && d.Status == CalendarDayStatus.Active);
            dto.TotalPtmDays = days.Count(d => d.DayType == DayType.PTM && d.Status == CalendarDayStatus.Active);
            dto.TotalEmergencyClosures = days.Count(d => d.DayType == DayType.EmergencyClosure && d.Status == CalendarDayStatus.Active);
        }

        return new PagedResultDto<AcademicCalendarDto>(totalCount, dtos);
    }

    public async Task<AcademicCalendarDto> GetCalendarByIdAsync(Guid id)
    {
        var calendar = await _calendarRepository.GetAsync(id);
        var dto = ObjectMapper.Map<AcademicCalendar, AcademicCalendarDto>(calendar);
        
        var days = await _calendarDayRepository.GetListAsync(d => d.AcademicCalendarId == id);
        dto.TotalWorkingDays = days.Count(d => d.DayType == DayType.WorkingDay && d.Status == CalendarDayStatus.Active);
        dto.TotalHolidays = days.Count(d => d.DayType == DayType.Holiday && d.Status == CalendarDayStatus.Active);
        dto.TotalVacationDays = days.Count(d => d.DayType == DayType.Vacation && d.Status == CalendarDayStatus.Active);
        dto.TotalHalfDays = days.Count(d => d.DayType == DayType.HalfDay && d.Status == CalendarDayStatus.Active);
        dto.TotalEvents = days.Count(d => d.DayType == DayType.Event && d.Status == CalendarDayStatus.Active);
        dto.TotalExamDays = days.Count(d => d.DayType == DayType.Exam && d.Status == CalendarDayStatus.Active);
        dto.TotalPtmDays = days.Count(d => d.DayType == DayType.PTM && d.Status == CalendarDayStatus.Active);
        dto.TotalEmergencyClosures = days.Count(d => d.DayType == DayType.EmergencyClosure && d.Status == CalendarDayStatus.Active);
        
        return dto;
    }

    public async Task<AcademicCalendarDto> CreateCalendarAsync(CreateCalendarDto input)
    {
        await _academicMediumRepository.GetAsync(input.AcademicMediumId);
        var calendar = await _calendarManager.CreateAsync(
            input.AcademicMediumId,
            input.AcademicYearId,
            input.Name,
            input.StartDate.DateTime,
            input.EndDate.DateTime,
            input.Description,
            input.IsDefault
        );

        await _calendarRepository.InsertAsync(calendar);

        // Pre-populate weekends if setting weekend rule exists
        var settings = await _settingRepository.FirstOrDefaultAsync(s => s.AcademicMediumId == input.AcademicMediumId);
        if (settings != null)
        {
            await _calendarManager.GenerateWeekendDaysAsync(calendar, settings.WeekendRule);
            await _calendarRepository.UpdateAsync(calendar);
        }

        await _auditWriter.WriteAsync(
            calendar.Id,
            CalendarAuditAction.CalendarCreated,
            beforeJson: null,
            afterJson: System.Text.Json.JsonSerializer.Serialize(calendar),
            entityType: "Academic Calendar",
            entityName: calendar.Name,
            reason: "Calendar initial creation");

        return ObjectMapper.Map<AcademicCalendar, AcademicCalendarDto>(calendar);
    }

    public async Task<AcademicCalendarDto> UpdateCalendarAsync(Guid id, UpdateCalendarDto input)
    {
        var calendar = await _calendarRepository.GetAsync(id);
        var oldVal = System.Text.Json.JsonSerializer.Serialize(calendar);

        // Handle default switch
        if (input.IsDefault && !calendar.IsDefault)
        {
            var currentDefaults = await _calendarRepository.GetListAsync(c =>
                c.AcademicMediumId == calendar.AcademicMediumId && c.IsDefault);
            foreach (var def in currentDefaults)
            {
                def.UpdateInfo(def.Name, def.Description, false);
                await _calendarRepository.UpdateAsync(def);
            }
        }

        calendar.UpdateInfo(input.Name, input.Description, input.IsDefault);
        await _calendarRepository.UpdateAsync(calendar);

        await _auditWriter.WriteAsync(
            calendar.Id,
            CalendarAuditAction.CalendarUpdated,
            beforeJson: oldVal,
            afterJson: System.Text.Json.JsonSerializer.Serialize(calendar),
            entityType: "Academic Calendar",
            entityName: calendar.Name);

        return ObjectMapper.Map<AcademicCalendar, AcademicCalendarDto>(calendar);
    }

    public async Task DeleteCalendarAsync(Guid id)
    {
        var calendar = await _calendarRepository.GetAsync(id);
        if (calendar.Status == CalendarStatus.Published)
        {
            throw new UserFriendlyException("Cannot delete a published Academic Calendar. Archive it first.");
        }

        var oldVal = System.Text.Json.JsonSerializer.Serialize(calendar);
        await _calendarRepository.DeleteAsync(id);

        await _auditWriter.WriteAsync(
            calendar.Id,
            CalendarAuditAction.CalendarDeleted,
            beforeJson: oldVal,
            afterJson: null,
            entityType: "Academic Calendar",
            entityName: calendar.Name,
            reason: "Calendar deleted");
    }

    public async Task<AcademicCalendarDto> PublishCalendarAsync(Guid id)
    {
        var calendar = await _calendarRepository.GetAsync(id);
        var oldVal = System.Text.Json.JsonSerializer.Serialize(calendar);

        calendar.Publish(CurrentUser.Id ?? Guid.Empty);
        await _calendarRepository.UpdateAsync(calendar);

        await _auditWriter.WriteAsync(
            calendar.Id,
            CalendarAuditAction.CalendarPublished,
            beforeJson: oldVal,
            afterJson: System.Text.Json.JsonSerializer.Serialize(calendar),
            entityType: "Academic Calendar",
            entityName: calendar.Name);

        return ObjectMapper.Map<AcademicCalendar, AcademicCalendarDto>(calendar);
    }

    public async Task<AcademicCalendarDto> ArchiveCalendarAsync(Guid id)
    {
        var calendar = await _calendarRepository.GetAsync(id);
        var oldVal = System.Text.Json.JsonSerializer.Serialize(calendar);

        calendar.Archive();
        await _calendarRepository.UpdateAsync(calendar);

        await _auditWriter.WriteAsync(
            calendar.Id,
            CalendarAuditAction.CalendarArchived,
            beforeJson: oldVal,
            afterJson: System.Text.Json.JsonSerializer.Serialize(calendar),
            entityType: "Academic Calendar",
            entityName: calendar.Name);

        return ObjectMapper.Map<AcademicCalendar, AcademicCalendarDto>(calendar);
    }

    // ─── Calendar Days CRUD ─────────────────────────────────────────────────

    public async Task<List<AcademicCalendarDayDto>> GetCalendarDaysAsync(Guid calendarId, DayType? dayType, int? month, int? year)
    {
        var query = await _calendarDayRepository.GetQueryableAsync();
        query = query.Where(d => d.AcademicCalendarId == calendarId && d.Status == CalendarDayStatus.Active);

        if (dayType.HasValue)
        {
            query = query.Where(d => d.DayType == dayType.Value);
        }

        var allItems = query.ToList();
        var rangeEnds = new Dictionary<Guid, DateTime?>();
        foreach (var group in allItems
                     .Where(d => d.DayType == DayType.Vacation)
                     .GroupBy(d => new { d.AcademicCalendarId, d.DayType, d.Title, d.AppliesToAll, d.StandardId, d.DivisionId }))
        {
            var orderedDays = group.OrderBy(d => d.Date).ToList();
            var rangeStartIndex = 0;
            for (var index = 1; index <= orderedDays.Count; index++)
            {
                var isSameRange = index < orderedDays.Count &&
                                  orderedDays[index].Date.Date == orderedDays[index - 1].Date.Date.AddDays(1);
                if (isSameRange)
                {
                    continue;
                }

                var rangeEnd = orderedDays[index - 1].Date.Date;
                for (var rangeIndex = rangeStartIndex; rangeIndex < index; rangeIndex++)
                {
                    rangeEnds[orderedDays[rangeIndex].Id] = rangeEnd;
                }

                rangeStartIndex = index;
            }
        }

        var items = allItems;

        if (month.HasValue)
        {
            items = items.Where(d => d.Date.Month == month.Value).ToList();
        }

        if (year.HasValue)
        {
            items = items.Where(d => d.Date.Year == year.Value).ToList();
        }

        var result = ObjectMapper.Map<List<AcademicCalendarDay>, List<AcademicCalendarDayDto>>(items.OrderBy(d => d.Date).ToList());
        foreach (var dto in result)
        {
            if (rangeEnds.TryGetValue(dto.Id, out var endDate))
            {
                dto.EndDate = endDate;
            }
        }

        return result;
    }

    public async Task<AcademicCalendarDayDto> CreateCalendarDayAsync(Guid calendarId, CreateCalendarDayDto input)
    {
        var calendar = await _calendarRepository.GetAsync(calendarId);
        var details = NormalizeDayDetails(input);
        var resolved = await ResolveExamScheduleAsync(calendar, details, input.Date.DateTime.Date);
        details = resolved.Details;
        await ValidateRoomAvailabilityAsync(details, resolved.Date, input.StandardId, input.DivisionId);
        var startDate = resolved.Date;
        var endDate = input.DayType == DayType.Vacation
            ? (input.EndDate ?? input.Date).DateTime.Date
            : startDate;

        if (endDate < startDate)
        {
            throw new UserFriendlyException("Vacation end date cannot be before the start date.");
        }

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            await _calendarManager.ValidateDayRangeAsync(calendar, date);
        }

        // One row per targeted class; a single null entry means the marker applies to all
        var targetStandardIds = input.AppliesToAll
            ? new List<Guid?> { null }
            : input.StandardIds is { Count: > 0 }
                ? input.StandardIds.Select(s => (Guid?)s).Distinct().ToList()
                : new List<Guid?> { input.StandardId };

        // Division is only meaningful when the marker targets a single class
        var divisionId = !input.AppliesToAll && targetStandardIds.Count == 1 ? input.DivisionId : null;

        foreach (var date in EnumerateDates(startDate, endDate))
        {
            foreach (var standardId in targetStandardIds)
            {
                var duplicate = await _calendarDayRepository.FirstOrDefaultAsync(d =>
                    d.AcademicCalendarId == calendarId && d.Date.Date == date.Date &&
                    d.Status == CalendarDayStatus.Active &&
                    (d.AppliesToAll || input.AppliesToAll || d.StandardId == standardId));

                if (duplicate != null)
                {
                    throw new UserFriendlyException($"A calendar day configuration already exists for {date:yyyy-MM-dd}. Delete or update it instead.");
                }
            }
        }

        AcademicCalendarDay? firstDay = null;
        foreach (var date in EnumerateDates(startDate, endDate))
        {
            foreach (var standardId in targetStandardIds)
            {
                var dayId = GuidGenerator.Create();
                var day = new AcademicCalendarDay(
                    dayId,
                    CurrentTenant.Id,
                    calendarId,
                    date,
                    input.DayType,
                    input.Title,
                    input.Description,
                    input.AppliesToAll,
                    standardId,
                    divisionId,
                    input.HolidayCategory,
                    details.StartTime,
                    details.EndTime,
                    venue: details.Venue,
                    examTypeId: details.ExamTypeId,
                    linkedExamId: details.LinkedExamId,
                    examStartTime: details.ExamStartTime,
                    examEndTime: details.ExamEndTime,
                    ptmMode: details.PtmMode,
                    roomId: details.RoomId,
                    meetingLink: details.MeetingLink
                );

                await _calendarDayRepository.InsertAsync(day);
                firstDay ??= day;

                // Generate impact logs only when the calendar is already published (nothing to
                // reschedule for a draft), but audit the change regardless of publish status.
                if (calendar.Status == CalendarStatus.Published)
                {
                    var impacts = await _calendarManager.ScanImpactsAsync(day);
                    foreach (var imp in impacts)
                    {
                        await _impactLogRepository.InsertAsync(imp);
                    }
                }

                await _auditWriter.WriteAsync(
                    calendarId,
                    CalendarAuditAction.DayAdded,
                    beforeJson: null,
                    afterJson: System.Text.Json.JsonSerializer.Serialize(day),
                    entityType: day.DayType.ToString(),
                    entityName: day.Title,
                    reason: input.ChangeReason,
                    academicCalendarDayId: day.Id);
            }
        }

        var result = ObjectMapper.Map<AcademicCalendarDay, AcademicCalendarDayDto>(firstDay!);
        result.EndDate = input.DayType == DayType.Vacation ? endDate : null;
        return result;
    }

    private static CalendarDayDetails NormalizeDayDetails(CreateCalendarDayDto input)
    {
        var dayType = input.DayType;

        if (dayType is DayType.Event or DayType.HalfDay or DayType.PTM)
        {
            if (dayType == DayType.HalfDay && (!input.StartTime.HasValue || !input.EndTime.HasValue))
            {
                throw new UserFriendlyException("Both start time and end time are required for a half day.");
            }

            if (input.StartTime.HasValue && input.EndTime.HasValue && input.EndTime <= input.StartTime)
            {
                throw new UserFriendlyException("End time must be later than start time.");
            }
        }

        if (dayType == DayType.Exam)
        {
            if (!input.ExamTypeId.HasValue || input.ExamTypeId == Guid.Empty)
            {
                throw new UserFriendlyException("Exam type is required for an exam day.");
            }

            if (!input.LinkedExamId.HasValue)
            {
                return new CalendarDayDetails(null, null, null, input.ExamTypeId, null, null, null, null, null, null);
            }

            return new CalendarDayDetails(null, null, null, input.ExamTypeId, input.LinkedExamId, null, null, null, null, null);
        }

        if (dayType == DayType.PTM)
        {
            if (string.IsNullOrWhiteSpace(input.PtmMode))
            {
                throw new UserFriendlyException("PTM mode is required.");
            }

            var mode = input.PtmMode.Trim();
            var supportsRoom = mode is "inPerson" or "hybrid";
            var supportsMeetingLink = mode is "online" or "hybrid";
            if (!supportsRoom && !supportsMeetingLink)
            {
                throw new UserFriendlyException("PTM mode must be inPerson, online, or hybrid.");
            }

            if (supportsRoom && (!input.RoomId.HasValue || input.RoomId == Guid.Empty))
            {
                throw new UserFriendlyException("Room is required for an in-person PTM.");
            }

            if (supportsMeetingLink && string.IsNullOrWhiteSpace(input.MeetingLink))
            {
                throw new UserFriendlyException("Meeting link is required for an online PTM.");
            }

            return new CalendarDayDetails(null, input.StartTime, input.EndTime, null, null, null, null, mode, input.RoomId, input.MeetingLink);
        }

        return new CalendarDayDetails(
            dayType == DayType.Event ? input.Venue : null,
            dayType is DayType.Event or DayType.HalfDay ? input.StartTime : null,
            dayType is DayType.Event or DayType.HalfDay ? input.EndTime : null,
            null, null, null, null, null, null, null);
    }

    private sealed record CalendarDayDetails(
        string? Venue,
        TimeSpan? StartTime,
        TimeSpan? EndTime,
        Guid? ExamTypeId,
        Guid? LinkedExamId,
        TimeSpan? ExamStartTime,
        TimeSpan? ExamEndTime,
        string? PtmMode,
        Guid? RoomId,
        string? MeetingLink);

    private async Task<(CalendarDayDetails Details, DateTime Date)> ResolveExamScheduleAsync(
        AcademicCalendar calendar,
        CalendarDayDetails details,
        DateTime requestedDate)
    {
        var exam = await ValidateExamReferencesAsync(calendar, details);
        if (exam == null)
        {
            return (details, requestedDate);
        }

        var scheduledSubject = exam.ExamSubjects
            .Where(subject => subject.ExamDate.HasValue)
            .OrderBy(subject => subject.ExamDate)
            .FirstOrDefault();

        if (scheduledSubject?.ExamDate == null ||
            !scheduledSubject.StartTime.HasValue ||
            !scheduledSubject.EndTime.HasValue)
        {
            throw new UserFriendlyException("The selected exam does not have a complete scheduled date and time.");
        }

        if (scheduledSubject.EndTime <= scheduledSubject.StartTime)
        {
            throw new UserFriendlyException("The selected exam has an invalid schedule: end time must be later than start time.");
        }

        return (
            details with
            {
                ExamStartTime = scheduledSubject.StartTime.Value.TimeOfDay,
                ExamEndTime = scheduledSubject.EndTime.Value.TimeOfDay
            },
            scheduledSubject.ExamDate.Value.Date);
    }

    private async Task ValidateRoomAvailabilityAsync(
        CalendarDayDetails details,
        DateTime date,
        Guid? standardId,
        Guid? divisionId)
    {
        if (!details.RoomId.HasValue || !details.StartTime.HasValue || !details.EndTime.HasValue)
        {
            return;
        }

        var conflict = await _roomConflictService.CheckConflictsAsync(
            details.RoomId.Value,
            date,
            details.StartTime.Value,
            details.EndTime.Value,
            standardId: standardId,
            divisionId: divisionId);

        if (conflict.OverallSeverity == "critical")
        {
            var message = conflict.Conflicts.FirstOrDefault()?.Message
                ?? "The selected room is not available for this time.";
            throw new UserFriendlyException(message);
        }
    }

    private async Task<Exam?> ValidateExamReferencesAsync(AcademicCalendar calendar, CalendarDayDetails details)
    {
        if (!details.ExamTypeId.HasValue)
        {
            return null;
        }

        var examType = await _examTypeRepository.GetAsync(details.ExamTypeId.Value);
        if (examType.AcademicMediumId != calendar.AcademicMediumId)
        {
            throw new UserFriendlyException("The selected exam type does not belong to this academic medium.");
        }

        if (!details.LinkedExamId.HasValue)
        {
            return null;
        }

        var exam = await _examRepository.GetAsync(details.LinkedExamId.Value, includeDetails: true);
        var standard = await _standardRepository.GetAsync(exam.StandardId);
        var stage = await _educationMediumStageRepository.GetAsync(standard.EducationMediumStageId);
        if (stage.EducationMediumId != calendar.AcademicMediumId)
        {
            throw new UserFriendlyException("The selected exam does not belong to this academic medium.");
        }

        return exam;
    }

    private static IEnumerable<DateTime> EnumerateDates(DateTime startDate, DateTime endDate)
    {
        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    public async Task<List<AcademicCalendarDayDto>> CreateCalendarDaysBulkAsync(Guid calendarId, CreateCalendarDaysBulkDto input)
    {
        var result = new List<AcademicCalendarDayDto>();
        foreach (var item in input.Days)
        {
            var day = await CreateCalendarDayAsync(calendarId, item);
            result.Add(day);
        }
        return result;
    }

    public async Task<AcademicCalendarDayDto> UpdateCalendarDayAsync(Guid id, CreateCalendarDayDto input)
    {
        var day = await _calendarDayRepository.GetAsync(id);
        var oldVal = System.Text.Json.JsonSerializer.Serialize(day);
        var calendar = await _calendarRepository.GetAsync(day.AcademicCalendarId);
        var details = NormalizeDayDetails(input);
        var resolved = await ResolveExamScheduleAsync(calendar, details, input.Date.DateTime.Date);
        details = resolved.Details;
        var targetDate = resolved.Date;
        await ValidateRoomAvailabilityAsync(details, targetDate, input.StandardId, input.DivisionId);

        if (input.DayType == DayType.Vacation)
        {
            return await UpdateVacationRangeAsync(day, input, calendar, targetDate);
        }

        await _calendarManager.ValidateDayRangeAsync(calendar, targetDate);

        // Sibling rows created together for a multi-class marker (same date, title, day type)
        var siblings = await _calendarDayRepository.GetListAsync(d =>
            d.AcademicCalendarId == day.AcademicCalendarId &&
            d.Date.Date == day.Date.Date &&
            d.Id != day.Id &&
            d.Status == CalendarDayStatus.Active &&
            !d.AppliesToAll &&
            d.Title == day.Title &&
            d.DayType == day.DayType);

        var targetStandardIds = input.AppliesToAll
            ? new List<Guid>()
            : input.StandardIds is { Count: > 0 }
                ? input.StandardIds.Distinct().ToList()
                : input.StandardId.HasValue
                    ? new List<Guid> { input.StandardId.Value }
                    : new List<Guid>();

        if (!input.AppliesToAll && targetStandardIds.Count == 0)
        {
            throw new UserFriendlyException("Select at least one class when the marker does not apply to all.");
        }

        // Division is only meaningful when the marker targets a single class
        var divisionId = !input.AppliesToAll && targetStandardIds.Count == 1 ? input.DivisionId : null;

        var group = new List<AcademicCalendarDay> { day };
        group.AddRange(siblings);
        var groupIds = group.Select(x => x.Id).ToHashSet();

        if (input.AppliesToAll)
        {
            var duplicate = await _calendarDayRepository.FirstOrDefaultAsync(d =>
                d.AcademicCalendarId == day.AcademicCalendarId &&
                d.Date.Date == targetDate &&
                d.Status == CalendarDayStatus.Active &&
                !groupIds.Contains(d.Id));

            if (duplicate != null)
            {
                throw new UserFriendlyException($"A calendar day configuration already exists for {targetDate:yyyy-MM-dd}. Delete or update it instead.");
            }
        }
        else
        {
            foreach (var standardId in targetStandardIds)
            {
                var duplicate = await _calendarDayRepository.FirstOrDefaultAsync(d =>
                    d.AcademicCalendarId == day.AcademicCalendarId &&
                    d.Date.Date == targetDate &&
                    d.Status == CalendarDayStatus.Active &&
                    !groupIds.Contains(d.Id) &&
                    (d.AppliesToAll || d.StandardId == standardId));

                if (duplicate != null)
                {
                    throw new UserFriendlyException($"A calendar day configuration already exists for {targetDate:yyyy-MM-dd}. Delete or update it instead.");
                }
            }
        }

        AcademicCalendarDay? resultDay = null;

        if (input.AppliesToAll)
        {
            foreach (var sibling in siblings)
            {
                await _calendarDayRepository.DeleteAsync(sibling.Id);
            }

            day.Update(
                targetDate,
                input.DayType,
                input.Title,
                input.Description,
                appliesToAll: true,
                standardId: null,
                divisionId: null,
                input.HolidayCategory,
                details.StartTime,
                details.EndTime,
                input.ChangeReason,
                venue: details.Venue,
                examTypeId: details.ExamTypeId,
                linkedExamId: details.LinkedExamId,
                examStartTime: details.ExamStartTime,
                examEndTime: details.ExamEndTime,
                ptmMode: details.PtmMode,
                roomId: details.RoomId,
                meetingLink: details.MeetingLink
            );
            await _calendarDayRepository.UpdateAsync(day);
            resultDay = day;
        }
        else
        {
            // Reconcile marker rows against the requested classes: keep rows whose class
            // is still selected, reassign or delete the rest, and add rows for new classes.
            var remaining = new List<Guid>(targetStandardIds);
            var unmatched = new List<AcademicCalendarDay>();
            foreach (var row in group)
            {
                if (row.StandardId.HasValue && remaining.Remove(row.StandardId.Value))
                {
                    row.Update(
                        targetDate,
                        input.DayType,
                        input.Title,
                        input.Description,
                        appliesToAll: false,
                        row.StandardId,
                        divisionId,
                        input.HolidayCategory,
                        details.StartTime,
                        details.EndTime,
                        input.ChangeReason,
                        venue: details.Venue,
                        examTypeId: details.ExamTypeId,
                        linkedExamId: details.LinkedExamId,
                        examStartTime: details.ExamStartTime,
                        examEndTime: details.ExamEndTime,
                        ptmMode: details.PtmMode,
                        roomId: details.RoomId,
                        meetingLink: details.MeetingLink
                    );
                    await _calendarDayRepository.UpdateAsync(row);
                    resultDay ??= row;
                }
                else
                {
                    unmatched.Add(row);
                }
            }

            foreach (var row in unmatched)
            {
                if (remaining.Count > 0)
                {
                    var reassignedStandardId = remaining[0];
                    remaining.RemoveAt(0);
                    row.Update(
                        targetDate,
                        input.DayType,
                        input.Title,
                        input.Description,
                        appliesToAll: false,
                        reassignedStandardId,
                        divisionId,
                        input.HolidayCategory,
                        details.StartTime,
                        details.EndTime,
                        input.ChangeReason,
                        venue: details.Venue,
                        examTypeId: details.ExamTypeId,
                        linkedExamId: details.LinkedExamId,
                        examStartTime: details.ExamStartTime,
                        examEndTime: details.ExamEndTime,
                        ptmMode: details.PtmMode,
                        roomId: details.RoomId,
                        meetingLink: details.MeetingLink
                    );
                    await _calendarDayRepository.UpdateAsync(row);
                    resultDay ??= row;
                }
                else
                {
                    await _calendarDayRepository.DeleteAsync(row.Id);
                }
            }

            foreach (var newStandardId in remaining)
            {
                var newDay = new AcademicCalendarDay(
                    GuidGenerator.Create(),
                    CurrentTenant.Id,
                    day.AcademicCalendarId,
                    targetDate,
                    input.DayType,
                    input.Title,
                    input.Description,
                    appliesToAll: false,
                    standardId: newStandardId,
                    divisionId: divisionId,
                    holidayCategory: input.HolidayCategory,
                    startTime: details.StartTime,
                    endTime: details.EndTime,
                    venue: details.Venue,
                    examTypeId: details.ExamTypeId,
                    linkedExamId: details.LinkedExamId,
                    examStartTime: details.ExamStartTime,
                    examEndTime: details.ExamEndTime,
                    ptmMode: details.PtmMode,
                    roomId: details.RoomId,
                    meetingLink: details.MeetingLink
                );
                await _calendarDayRepository.InsertAsync(newDay);
                resultDay ??= newDay;
            }
        }

        resultDay ??= day;

        await _auditWriter.WriteAsync(
            resultDay.AcademicCalendarId,
            CalendarAuditAction.DayUpdated,
            beforeJson: oldVal,
            afterJson: System.Text.Json.JsonSerializer.Serialize(resultDay),
            entityType: resultDay.DayType.ToString(),
            entityName: resultDay.Title,
            reason: input.ChangeReason,
            academicCalendarDayId: resultDay.Id);

        return ObjectMapper.Map<AcademicCalendarDay, AcademicCalendarDayDto>(resultDay);
    }

    private async Task<AcademicCalendarDayDto> UpdateVacationRangeAsync(
        AcademicCalendarDay day,
        CreateCalendarDayDto input,
        AcademicCalendar calendar,
        DateTime startDate)
    {
        var endDate = (input.EndDate ?? input.Date).DateTime.Date;
        if (endDate < startDate)
        {
            throw new UserFriendlyException("Vacation end date cannot be before the start date.");
        }

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            await _calendarManager.ValidateDayRangeAsync(calendar, date);
        }

        var targetStandardIds = input.AppliesToAll
            ? new List<Guid?> { null }
            : input.StandardIds is { Count: > 0 }
                ? input.StandardIds.Distinct().Select(id => (Guid?)id).ToList()
                : new List<Guid?> { input.StandardId };

        if (!input.AppliesToAll && targetStandardIds.All(id => !id.HasValue))
        {
            throw new UserFriendlyException("Select at least one class when the marker does not apply to all.");
        }

        var existingRows = await _calendarDayRepository.GetListAsync(d =>
            d.AcademicCalendarId == day.AcademicCalendarId &&
            d.Status == CalendarDayStatus.Active &&
            d.DayType == DayType.Vacation &&
            d.Title == day.Title &&
            (d.AppliesToAll == day.AppliesToAll) &&
            (day.AppliesToAll || d.StandardId == day.StandardId ||
                (input.StandardIds != null && d.StandardId.HasValue && input.StandardIds.Contains(d.StandardId.Value))));

        var desired = new HashSet<(DateTime Date, Guid? StandardId)>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            foreach (var standardId in targetStandardIds)
            {
                desired.Add((date, standardId));
            }
        }

        AcademicCalendarDay? resultDay = null;
        foreach (var existing in existingRows)
        {
            var key = (existing.Date.Date, existing.StandardId);
            if (!desired.Remove(key))
            {
                await _calendarDayRepository.DeleteAsync(existing.Id);
                continue;
            }

            existing.Update(
                existing.Date,
                DayType.Vacation,
                input.Title,
                input.Description,
                input.AppliesToAll,
                input.AppliesToAll ? null : existing.StandardId,
                input.AppliesToAll ? null : targetStandardIds.Count == 1 ? input.DivisionId : null,
                input.HolidayCategory,
                null,
                null,
                input.ChangeReason,
                venue: null,
                examTypeId: null,
                linkedExamId: null,
                examStartTime: null,
                examEndTime: null,
                ptmMode: null,
                roomId: null,
                meetingLink: null);
            await _calendarDayRepository.UpdateAsync(existing);
            resultDay ??= existing;
        }

        foreach (var (date, standardId) in desired)
        {
            var newDay = new AcademicCalendarDay(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                day.AcademicCalendarId,
                date,
                DayType.Vacation,
                input.Title,
                input.Description,
                input.AppliesToAll,
                standardId,
                input.AppliesToAll || targetStandardIds.Count != 1 ? null : input.DivisionId,
                input.HolidayCategory);
            await _calendarDayRepository.InsertAsync(newDay);
            resultDay ??= newDay;
        }

        resultDay ??= day;
        await _auditWriter.WriteAsync(
            day.AcademicCalendarId,
            CalendarAuditAction.DayUpdated,
            beforeJson: null,
            afterJson: System.Text.Json.JsonSerializer.Serialize(resultDay),
            entityType: resultDay.DayType.ToString(),
            entityName: resultDay.Title,
            reason: input.ChangeReason,
            academicCalendarDayId: resultDay.Id);

        var result = ObjectMapper.Map<AcademicCalendarDay, AcademicCalendarDayDto>(resultDay);
        result.EndDate = endDate;
        return result;
    }

    public async Task DeleteCalendarDayAsync(Guid id)
    {
        var day = await _calendarDayRepository.GetAsync(id);
        var oldVal = System.Text.Json.JsonSerializer.Serialize(day);

        await _calendarDayRepository.DeleteAsync(id);

        await _auditWriter.WriteAsync(
            day.AcademicCalendarId,
            CalendarAuditAction.DayDeleted,
            beforeJson: oldVal,
            afterJson: null,
            entityType: day.DayType.ToString(),
            entityName: day.Title,
            reason: "Day configuration deleted",
            academicCalendarDayId: day.Id);
    }

    // ─── Working Hours ──────────────────────────────────────────────────────

    public async Task<List<AcademicCalendarWorkingHourDto>> GetWorkingHoursAsync(Guid calendarId)
    {
        var items = await _workingHourRepository.GetListAsync(w => w.AcademicCalendarId == calendarId);
        return ObjectMapper.Map<List<AcademicCalendarWorkingHour>, List<AcademicCalendarWorkingHourDto>>(items);
    }

    public async Task<AcademicCalendarWorkingHourDto> CreateWorkingHourAsync(Guid calendarId, AcademicCalendarWorkingHourDto input)
    {
        var id = GuidGenerator.Create();
        var wh = new AcademicCalendarWorkingHour(
            id,
            CurrentTenant.Id,
            calendarId,
            input.StartTime,
            input.EndTime,
            input.ShiftType,
            input.StandardId,
            input.DivisionId,
            input.RecessStartTime,
            input.RecessEndTime,
            input.IsDefault
        );
        await _workingHourRepository.InsertAsync(wh);

        await _auditWriter.WriteAsync(
            wh.AcademicCalendarId,
            CalendarAuditAction.WorkingHourAdded,
            beforeJson: null,
            afterJson: System.Text.Json.JsonSerializer.Serialize(wh),
            entityType: "Working Hours",
            entityName: $"{wh.ShiftType} shift ({wh.StartTime:hh\\:mm}-{wh.EndTime:hh\\:mm})");

        return ObjectMapper.Map<AcademicCalendarWorkingHour, AcademicCalendarWorkingHourDto>(wh);
    }

    public async Task<AcademicCalendarWorkingHourDto> UpdateWorkingHourAsync(Guid id, AcademicCalendarWorkingHourDto input)
    {
        var wh = await _workingHourRepository.GetAsync(id);
        var oldVal = System.Text.Json.JsonSerializer.Serialize(wh);

        wh.Update(
            input.StartTime,
            input.EndTime,
            input.ShiftType,
            input.StandardId,
            input.DivisionId,
            input.RecessStartTime,
            input.RecessEndTime,
            input.IsDefault
        );
        await _workingHourRepository.UpdateAsync(wh);

        await _auditWriter.WriteAsync(
            wh.AcademicCalendarId,
            CalendarAuditAction.WorkingHourUpdated,
            beforeJson: oldVal,
            afterJson: System.Text.Json.JsonSerializer.Serialize(wh),
            entityType: "Working Hours",
            entityName: $"{wh.ShiftType} shift ({wh.StartTime:hh\\:mm}-{wh.EndTime:hh\\:mm})");

        return ObjectMapper.Map<AcademicCalendarWorkingHour, AcademicCalendarWorkingHourDto>(wh);
    }

    public async Task DeleteWorkingHourAsync(Guid id)
    {
        var wh = await _workingHourRepository.GetAsync(id);
        var oldVal = System.Text.Json.JsonSerializer.Serialize(wh);

        await _workingHourRepository.DeleteAsync(id);

        await _auditWriter.WriteAsync(
            wh.AcademicCalendarId,
            CalendarAuditAction.WorkingHourDeleted,
            beforeJson: oldVal,
            afterJson: null,
            entityType: "Working Hours",
            entityName: $"{wh.ShiftType} shift ({wh.StartTime:hh\\:mm}-{wh.EndTime:hh\\:mm})");
    }

    // ─── Date Validation ────────────────────────────────────────────────────

    public async Task<CheckDateResponseDto> CheckDateAsync(CheckDateQueryDto input)
    {
        var inputDate = input.Date.DateTime;
        // Find the default active calendar for the requested medium and date range first.
        var calendar = await _calendarRepository.FirstOrDefaultAsync(c =>
            (!input.AcademicMediumId.HasValue || c.AcademicMediumId == input.AcademicMediumId.Value) &&
            c.Status == CalendarStatus.Published &&
            inputDate.Date >= c.StartDate.Date &&
            inputDate.Date <= c.EndDate.Date);

        if (calendar == null)
        {
            return new CheckDateResponseDto
            {
                IsAllowed = true,
                Date = inputDate,
                Message = "No active published academic calendar covers this date. Date is allowed by default."
            };
        }

        // Search for day config
        var day = await _calendarDayRepository.FirstOrDefaultAsync(d =>
            d.AcademicCalendarId == calendar.Id &&
            d.Date.Date == inputDate.Date &&
            d.Status == CalendarDayStatus.Active &&
            (d.AppliesToAll || 
             (input.StandardId.HasValue && d.StandardId == input.StandardId.Value && 
              (!d.DivisionId.HasValue || (input.DivisionId.HasValue && d.DivisionId == input.DivisionId.Value)))));

        if (day == null)
        {
            return new CheckDateResponseDto
            {
                IsAllowed = true,
                Date = inputDate,
                Message = "Active working day."
            };
        }

        // Apply rules based on validation request type
        bool isBlocked = CalendarDayBlockingRules.Blocks(day.DayType, input.ValidationType);
        string blockingReason = input.ValidationType.ToLowerInvariant() == "attendance"
            ? "AttendanceExemption"
            : day.DayType.ToString();

        if (isBlocked)
        {
            // Suggest next working day (recursive scan)
            var nextWorking = inputDate.AddDays(1);
            while (nextWorking <= calendar.EndDate)
            {
                var nextDay = await _calendarDayRepository.FirstOrDefaultAsync(d =>
                    d.AcademicCalendarId == calendar.Id &&
                    d.Date.Date == nextWorking.Date &&
                    d.Status == CalendarDayStatus.Active &&
                    (d.AppliesToAll || (input.StandardId.HasValue && d.StandardId == input.StandardId.Value)));
                
                bool nextBlocked = nextDay != null
                    && CalendarDayBlockingRules.Blocks(nextDay.DayType, input.ValidationType);

                if (!nextBlocked)
                {
                    break;
                }
                nextWorking = nextWorking.AddDays(1);
            }

            return new CheckDateResponseDto
            {
                IsAllowed = false,
                Date = inputDate,
                DayType = day.DayType,
                Title = day.Title,
                Message = $"Date blocked: {inputDate:MMM dd} falls on {day.Title} ({day.DayType}).",
                BlockingReason = blockingReason,
                SuggestedNextWorkingDate = nextWorking
            };
        }

        return new CheckDateResponseDto
        {
            IsAllowed = true,
            Date = inputDate,
            DayType = day.DayType,
            Title = day.Title,
            Message = "Date is allowed."
        };
    }

    // ─── Emergency Closures ─────────────────────────────────────────────────

    [AllowAnonymous]
    public async Task<PagedResultDto<EmergencyClosureListItemDto>> GetEmergencyClosuresAsync(
        Guid? academicMediumId,
        CalendarDayStatus? status,
        int skipCount = 0,
        int maxResultCount = 10)
    {
        var dayQuery = await _calendarDayRepository.GetQueryableAsync();
        var calendarQuery = await _calendarRepository.GetQueryableAsync();
        var query =
            from day in dayQuery
            join calendar in calendarQuery on day.AcademicCalendarId equals calendar.Id
            where day.DayType == DayType.EmergencyClosure
            select new { day, calendar };

        if (academicMediumId.HasValue)
        {
            query = query.Where(x => x.calendar.AcademicMediumId == academicMediumId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.day.Status == status.Value);
        }

        var totalCount = query.Count();
        var rows = query
            .OrderByDescending(x => x.day.Date)
            .ThenByDescending(x => x.day.CreationTime)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToList();

        var dayIds = rows.Select(x => x.day.Id).ToList();
        var impacts = await _impactLogRepository.GetListAsync(i => dayIds.Contains(i.AcademicCalendarDayId));
        var items = rows.Select(x =>
        {
            var dayImpacts = impacts.Where(i => i.AcademicCalendarDayId == x.day.Id).ToList();
            return new EmergencyClosureListItemDto
            {
                Id = x.day.Id,
                AcademicCalendarId = x.day.AcademicCalendarId,
                CalendarName = x.calendar.Name,
                Date = x.day.Date,
                StartTime = x.day.StartTime,
                EndTime = x.day.EndTime,
                Title = x.day.Title,
                Description = x.day.Description,
                AppliesToAll = x.day.AppliesToAll,
                StandardId = x.day.StandardId,
                DivisionId = x.day.DivisionId,
                Status = x.day.Status,
                ChangeReason = x.day.ChangeReason,
                AutoScanImpacts = x.day.AutoScanImpacts,
                ImpactCount = dayImpacts.Count,
                PendingImpactCount = dayImpacts.Count(i =>
                    i.Status == CalendarImpactStatus.Pending ||
                    i.Status == CalendarImpactStatus.Suggested)
            };
        }).ToList();

        return new PagedResultDto<EmergencyClosureListItemDto>(totalCount, items);
    }

    [AllowAnonymous]
    public async Task<List<AcademicCalendarDayDto>> CreateEmergencyClosureAsync(CreateEmergencyClosureDto input)
    {
        var startDate = (input.StartDate ?? input.Date).DateTime.Date;
        var endDate = (input.EndDate ?? input.StartDate ?? input.Date).DateTime.Date;

        if (endDate < startDate)
        {
            throw new UserFriendlyException("Closure end date cannot be before the start date.");
        }

        if (input.StartTime.HasValue != input.EndTime.HasValue)
        {
            throw new UserFriendlyException("Both start time and end time are required for a partial-day closure.");
        }

        if (input.StartTime.HasValue && input.EndTime.HasValue && input.EndTime <= input.StartTime)
        {
            throw new UserFriendlyException("Closure end time must be after the start time.");
        }

        var results = new List<AcademicCalendarDayDto>();
        for (var closureDate = startDate; closureDate <= endDate; closureDate = closureDate.AddDays(1))
        {
            var calendar = await ResolveCalendarForEmergencyClosureAsync(input.AcademicMediumId, closureDate);
            await CreateEmergencyClosureForDateAsync(input, closureDate, calendar, results);
        }

        return results;
    }

    public async Task<AcademicCalendarDayDto> UpdateEmergencyClosureAsync(Guid dayId, UpdateEmergencyClosureDto input)
    {
        var day = await _calendarDayRepository.GetAsync(dayId);
        var targetDate = (input.StartDate ?? input.EndDate)?.DateTime.Date ?? day.Date.Date;
        if (day.DayType != DayType.EmergencyClosure)
        {
            throw new UserFriendlyException("Selected day is not an emergency closure.");
        }

        if (day.Status != CalendarDayStatus.Active)
        {
            throw new UserFriendlyException("Cannot update a cancelled emergency closure.");
        }

        if (input.StartTime.HasValue != input.EndTime.HasValue)
        {
            throw new UserFriendlyException("Both start time and end time are required for a partial-day closure.");
        }

        if (input.StartTime.HasValue && input.EndTime.HasValue && input.EndTime <= input.StartTime)
        {
            throw new UserFriendlyException("Closure end time must be after the start time.");
        }

        if (input.EndDate.HasValue && input.EndDate.Value.DateTime.Date < targetDate)
        {
            throw new UserFriendlyException("Closure end date cannot be before the start date.");
        }

        var calendar = await _calendarRepository.GetAsync(day.AcademicCalendarId);
        await _calendarManager.ValidateDayRangeAsync(calendar, targetDate);

        var targetStandardIds = input.AppliesToAll
            ? new List<Guid>()
            : input.StandardIds is { Count: > 0 }
                ? input.StandardIds.Distinct().ToList()
                : new List<Guid>();

        if (!input.AppliesToAll && targetStandardIds.Count == 0)
        {
            throw new UserFriendlyException("Select at least one class when the closure does not apply to all.");
        }

        if (input.AppliesToAll)
        {
            var duplicate = await _calendarDayRepository.FirstOrDefaultAsync(d =>
                d.AcademicCalendarId == day.AcademicCalendarId &&
                d.Date.Date == targetDate &&
                d.Status == CalendarDayStatus.Active &&
                d.Id != day.Id);

            if (duplicate != null)
            {
                throw new UserFriendlyException($"A calendar day configuration already exists for {targetDate:yyyy-MM-dd}. Delete or update it instead.");
            }
        }
        else
        {
            foreach (var standardId in targetStandardIds)
            {
                var duplicate = await _calendarDayRepository.FirstOrDefaultAsync(d =>
                    d.AcademicCalendarId == day.AcademicCalendarId &&
                    d.Date.Date == targetDate &&
                    d.Status == CalendarDayStatus.Active &&
                    d.Id != day.Id &&
                    (d.AppliesToAll || d.StandardId == standardId));

                if (duplicate != null)
                {
                    throw new UserFriendlyException($"A calendar day configuration already exists for {targetDate:yyyy-MM-dd}. Delete or update it instead.");
                }
            }
        }

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(day);

        day.Update(
            targetDate,
            DayType.EmergencyClosure,
            input.Title,
            input.Description,
            input.AppliesToAll,
            input.AppliesToAll ? null : targetStandardIds.FirstOrDefault(),
            null,
            HolidayCategory.WeatherEmergency,
            input.StartTime,
            input.EndTime,
            changeReason: "Emergency closure updated by administrator",
            autoScanImpacts: input.AutoScanImpacts
        );

        await _calendarDayRepository.UpdateAsync(day);

        await _auditWriter.WriteAsync(
            day.AcademicCalendarId,
            CalendarAuditAction.DayUpdated,
            beforeJson: beforeJson,
            afterJson: System.Text.Json.JsonSerializer.Serialize(day),
            entityType: "Emergency Closure",
            entityName: day.Title,
            reason: "Emergency closure updated by administrator",
            academicCalendarDayId: day.Id,
            module: "EmergencyClosure");

        return ObjectMapper.Map<AcademicCalendarDay, AcademicCalendarDayDto>(day);
    }

    private async Task CreateEmergencyClosureForDateAsync(
        CreateEmergencyClosureDto input,
        DateTime inputDate,
        AcademicCalendar calendar,
        List<AcademicCalendarDayDto> results)
    {
        // Create Day records for each standard/division if specific standards are requested
        if (!input.AppliesToAll && input.StandardIds != null && input.StandardIds.Count > 0)
        {
            foreach (var stdId in input.StandardIds)
            {
                var dayId = GuidGenerator.Create();
                var day = new AcademicCalendarDay(
                    dayId,
                    CurrentTenant.Id,
                    calendar.Id,
                    inputDate,
                    DayType.EmergencyClosure,
                    input.Title,
                    input.Description,
                    appliesToAll: false,
                    standardId: stdId,
                    divisionId: null,
                    holidayCategory: HolidayCategory.WeatherEmergency,
                    startTime: input.StartTime,
                    endTime: input.EndTime,
                    autoScanImpacts: input.AutoScanImpacts
                );
                await _calendarDayRepository.InsertAsync(day);
                results.Add(ObjectMapper.Map<AcademicCalendarDay, AcademicCalendarDayDto>(day));

                if (input.AutoScanImpacts)
                {
                    var impacts = await _calendarManager.ScanImpactsAsync(day);
                    foreach (var imp in impacts)
                    {
                        await _impactLogRepository.InsertAsync(imp);
                    }
                }

                await _auditWriter.WriteAsync(
                    calendar.Id,
                    CalendarAuditAction.EmergencyClosureDeclared,
                    beforeJson: null,
                    afterJson: System.Text.Json.JsonSerializer.Serialize(day),
                    entityType: "Emergency Closure",
                    entityName: day.Title,
                    reason: "Emergency Closure Declared for class: " + input.Title,
                    academicCalendarDayId: day.Id,
                    module: "EmergencyClosure");
            }
        }
        else
        {
            // Global emergency closure
            var dayId = GuidGenerator.Create();
            var day = new AcademicCalendarDay(
                dayId,
                CurrentTenant.Id,
                calendar.Id,
                inputDate,
                DayType.EmergencyClosure,
                input.Title,
                input.Description,
                appliesToAll: true,
                standardId: null,
                divisionId: null,
                holidayCategory: HolidayCategory.WeatherEmergency,
                startTime: input.StartTime,
                endTime: input.EndTime,
                autoScanImpacts: input.AutoScanImpacts
            );
            await _calendarDayRepository.InsertAsync(day);
            results.Add(ObjectMapper.Map<AcademicCalendarDay, AcademicCalendarDayDto>(day));

            if (input.AutoScanImpacts)
            {
                var impacts = await _calendarManager.ScanImpactsAsync(day);
                foreach (var imp in impacts)
                {
                    await _impactLogRepository.InsertAsync(imp);
                }
            }

            // Keep a distinct module for filtering while still surfacing it in calendar audit logs.
            await _auditWriter.WriteAsync(
                calendar.Id,
                CalendarAuditAction.EmergencyClosureDeclared,
                beforeJson: null,
                afterJson: System.Text.Json.JsonSerializer.Serialize(day),
                entityType: "Emergency Closure",
                entityName: day.Title,
                reason: "Emergency Closure Declared globally: " + input.Title,
                academicCalendarDayId: day.Id,
                module: "EmergencyClosure");
        }
    }

    private async Task<AcademicCalendar> ResolveCalendarForEmergencyClosureAsync(Guid academicMediumId, DateTime inputDate)
    {
        var requestedDate = inputDate.Date;
        var matchingCalendars = await _calendarRepository.GetListAsync(c =>
            c.AcademicMediumId == academicMediumId &&
            c.Status != CalendarStatus.Archived &&
            requestedDate >= c.StartDate.Date &&
            requestedDate <= c.EndDate.Date);
        var calendar = matchingCalendars
            .OrderBy(c => c.Status == CalendarStatus.Published ? 0 : c.IsDefault ? 1 : 2)
            .ThenByDescending(c => c.CreationTime)
            .FirstOrDefault();

        if (calendar != null)
        {
            return calendar;
        }

        var academicYear = await _academicYearRepository.FirstOrDefaultAsync(y =>
            requestedDate >= y.StartDate.Date &&
            requestedDate <= y.EndDate.Date);

        if (academicYear == null)
        {
            throw new UserFriendlyException("Cannot declare emergency closure: No academic year covers this date for the selected medium.");
        }

        var existingMediumCalendars = await _calendarRepository.GetListAsync(c =>
            c.AcademicMediumId == academicMediumId &&
            c.Status != CalendarStatus.Archived);
        var medium = await _academicMediumRepository.GetAsync(academicMediumId);
        var calendarName = BuildAutoCalendarName(medium.Name, existingMediumCalendars);
        var autoCalendar = await _calendarManager.CreateAsync(
            academicMediumId,
            academicYear.Id,
            calendarName,
            academicYear.StartDate,
            academicYear.EndDate,
            "System-created calendar for emergency closure entries.",
            isDefault: existingMediumCalendars.All(c => !c.IsDefault));

        var settings = await _settingRepository.FirstOrDefaultAsync(s => s.AcademicMediumId == academicMediumId);
        if (settings != null)
        {
            await _calendarManager.GenerateWeekendDaysAsync(autoCalendar, settings.WeekendRule);
        }

        await _calendarRepository.InsertAsync(autoCalendar);

        await _auditWriter.WriteAsync(
            autoCalendar.Id,
            CalendarAuditAction.CalendarCreated,
            beforeJson: null,
            afterJson: System.Text.Json.JsonSerializer.Serialize(autoCalendar),
            entityType: "Academic Calendar",
            entityName: autoCalendar.Name,
            reason: "Calendar auto-created while declaring emergency closure.",
            module: "EmergencyClosure");

        return autoCalendar;
    }

    private static string BuildAutoCalendarName(string mediumName, IReadOnlyCollection<AcademicCalendar> existingCalendars)
    {
        var baseName = $"{mediumName} Academic Calendar";
        if (existingCalendars.All(c => !string.Equals(c.Name, baseName, StringComparison.OrdinalIgnoreCase)))
        {
            return baseName;
        }

        var index = 2;
        string candidate;
        do
        {
            candidate = $"{baseName} {index}";
            index++;
        }
        while (existingCalendars.Any(c => string.Equals(c.Name, candidate, StringComparison.OrdinalIgnoreCase)));

        return candidate;
    }

    [AllowAnonymous]
    public async Task CancelEmergencyClosureAsync(Guid dayId)
    {
        var day = await _calendarDayRepository.GetAsync(dayId);
        if (day.DayType != DayType.EmergencyClosure)
        {
            throw new UserFriendlyException("Selected day is not an emergency closure.");
        }

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(day);
        day.Cancel("Emergency closure cancelled by administrator");
        await _calendarDayRepository.UpdateAsync(day);
        await _auditWriter.WriteAsync(
            day.AcademicCalendarId,
            CalendarAuditAction.DayUpdated,
            beforeJson: beforeJson,
            afterJson: System.Text.Json.JsonSerializer.Serialize(day),
            entityType: "Emergency Closure",
            entityName: day.Title,
            reason: "Emergency closure cancelled by administrator",
            academicCalendarDayId: day.Id,
            module: "EmergencyClosure");

        // Cancel associated impact logs
        var impacts = await _impactLogRepository.GetListAsync(i => i.AcademicCalendarDayId == dayId);
        foreach (var imp in impacts)
        {
            imp.Resolve(CurrentUser.Id ?? Guid.Empty, "Closure cancelled");
            await _impactLogRepository.UpdateAsync(imp);
        }
    }

    // ─── Impact Logs ────────────────────────────────────────────────────────

    public async Task<PagedResultDto<CalendarImpactLogDto>> GetImpactLogsAsync(
        Guid? calendarDayId,
        CalendarImpactType? impactType,
        CalendarImpactStatus? status,
        int skipCount = 0,
        int maxResultCount = 10)
    {
        var query = await _impactLogRepository.GetQueryableAsync();

        if (calendarDayId.HasValue)
        {
            query = query.Where(x => x.AcademicCalendarDayId == calendarDayId.Value);
        }

        if (impactType.HasValue)
        {
            query = query.Where(x => x.ImpactType == impactType.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(x => x.CreationTime)
                         .Skip(skipCount)
                         .Take(maxResultCount)
                         .ToList();

        return new PagedResultDto<CalendarImpactLogDto>(
            totalCount,
            ObjectMapper.Map<List<CalendarImpactLog>, List<CalendarImpactLogDto>>(items)
        );
    }

    public async Task<List<CalendarImpactLogDto>> GetImpactLogsByDayAsync(Guid calendarDayId)
    {
        var items = await _impactLogRepository.GetListAsync(x => x.AcademicCalendarDayId == calendarDayId);
        return ObjectMapper.Map<List<CalendarImpactLog>, List<CalendarImpactLogDto>>(items);
    }

    public async Task ResolveImpactLogAsync(Guid id, ResolveImpactLogDto input)
    {
        var log = await _impactLogRepository.GetAsync(id);
        log.Resolve(CurrentUser.Id ?? Guid.Empty, input.ResolutionNote);
        await _impactLogRepository.UpdateAsync(log);
    }

    public async Task IgnoreImpactLogAsync(Guid id, IgnoreImpactLogDto input)
    {
        var log = await _impactLogRepository.GetAsync(id);
        log.Ignore(input.Reason);
        await _impactLogRepository.UpdateAsync(log);
    }

    public async Task RescheduleImpactLogAsync(Guid id, RescheduleImpactLogDto input)
    {
        var log = await _impactLogRepository.GetAsync(id);
        log.Reschedule(input.NewDate.DateTime, input.Note);
        await _impactLogRepository.UpdateAsync(log);
    }

    // ─── Audit Logs ─────────────────────────────────────────────────────────

    public async Task<PagedResultDto<AcademicCalendarAuditLogDto>> GetAuditLogsAsync(
        Guid? calendarId,
        CalendarAuditAction? action,
        int skipCount = 0,
        int maxResultCount = 10)
    {
        var query = await _auditLogRepository.GetQueryableAsync();
        query = query.Where(x => x.Module == "CalendarManagement" || x.Module == "EmergencyClosure");

        if (calendarId.HasValue)
        {
            query = query.Where(x => x.AcademicCalendarId == calendarId.Value);
        }

        if (action.HasValue)
        {
            query = query.Where(x => x.Action == action.Value);
        }

        var totalCount = query.Count();
        var items = query.OrderByDescending(x => x.ChangedAt)
                         .Skip(skipCount)
                         .Take(maxResultCount)
                         .ToList();

        return new PagedResultDto<AcademicCalendarAuditLogDto>(
            totalCount,
            await MapAuditLogsWithReviewNotesAsync(items)
        );
    }

    public async Task<List<AcademicCalendarAuditLogDto>> GetAuditLogsByCalendarAsync(Guid calendarId)
    {
        var items = await _auditLogRepository.GetListAsync(x =>
            x.AcademicCalendarId == calendarId &&
            (x.Module == "CalendarManagement" || x.Module == "EmergencyClosure"));
        return await MapAuditLogsWithReviewNotesAsync(items.OrderByDescending(x => x.ChangedAt));
    }

    public async Task<List<AcademicCalendarAuditLogDto>> GetAuditLogsByDayAsync(Guid calendarDayId)
    {
        var items = await _auditLogRepository.GetListAsync(x =>
            x.AcademicCalendarDayId == calendarDayId &&
            (x.Module == "CalendarManagement" || x.Module == "EmergencyClosure"));
        return await MapAuditLogsWithReviewNotesAsync(items.OrderByDescending(x => x.ChangedAt));
    }

    public async Task<AuditLogReviewNoteDto> AddAuditLogReviewNoteAsync(Guid auditLogId, AddAuditLogReviewNoteDto input)
    {
        var auditLog = await _auditLogRepository.GetAsync(auditLogId);
        var noteText = input.Note.Trim();
        if (noteText.Length == 0)
        {
            throw new UserFriendlyException("Review note cannot be empty.");
        }

        var userId = CurrentUser.Id ?? throw new Volo.Abp.Authorization.AbpAuthorizationException(
            "A signed-in user is required to add a review note.");
        var note = new AcademicCalendarAuditReviewNote(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            auditLog.Id,
            userId,
            noteText,
            CurrentUser.Name ?? CurrentUser.UserName,
            CurrentUser.Roles.FirstOrDefault());

        await _auditReviewNoteRepository.InsertAsync(note, autoSave: true);
        return MapReviewNote(note);
    }

    private async Task<List<AcademicCalendarAuditLogDto>> MapAuditLogsWithReviewNotesAsync(IEnumerable<AcademicCalendarAuditLog> items)
    {
        var dtos = items.Select(AcademicCalendarAuditLogProjector.ToDto).ToList();
        if (dtos.Count == 0)
        {
            return dtos;
        }

        // Audit snapshots store IDs, but this is a human-facing API. Resolve every user
        // reference so fields such as PublishedByUserId never display opaque GUIDs.
        var userIds = dtos
            .Select(x => x.ChangedByUserId)
            .Concat(dtos.SelectMany(x => x.Diff)
                .Where(x => x.FieldName.EndsWith("UserId", StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => new[] { x.OldValue, x.NewValue })
                .Where(x => Guid.TryParse(x, out _))
                .Select(Guid.Parse))
            .Distinct()
            .ToList();

        var users = userIds.Count == 0
            ? []
            : await _identityUserRepository.GetListAsync(x => userIds.Contains(x.Id));
        var userNames = users.ToDictionary(x => x.Id, GetUserDisplayName);

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.ChangedByName) && userNames.TryGetValue(dto.ChangedByUserId, out var changedByName))
            {
                dto.ChangedByName = changedByName;
            }

            foreach (var field in dto.Diff.Where(x => x.FieldName.EndsWith("UserId", StringComparison.OrdinalIgnoreCase)))
            {
                field.DisplayName = field.DisplayName.Replace(" User Id", " User", StringComparison.OrdinalIgnoreCase);
                field.OldValue = ResolveUserName(field.OldValue, userNames);
                field.NewValue = ResolveUserName(field.NewValue, userNames);
            }

            foreach (var field in dto.Diff)
            {
                field.OldValue = ToDisplayValue(field.FieldName, field.OldValue, dto.EntityType);
                field.NewValue = ToDisplayValue(field.FieldName, field.NewValue, dto.EntityType);
            }
        }

        var auditLogIds = dtos.Select(x => x.Id).ToList();
        var notes = await _auditReviewNoteRepository.GetListAsync(x => auditLogIds.Contains(x.AuditLogId));
        var notesByAuditLog = notes
            .OrderBy(x => x.CreatedAt)
            .GroupBy(x => x.AuditLogId)
            .ToDictionary(x => x.Key, x => x.Select(MapReviewNote).ToList());

        foreach (var dto in dtos)
        {
            dto.ReviewNotes = notesByAuditLog.GetValueOrDefault(dto.Id) ?? new List<AuditLogReviewNoteDto>();
        }

        return dtos;
    }

    private static string? ResolveUserName(string? value, IReadOnlyDictionary<Guid, string> userNames) =>
        Guid.TryParse(value, out var userId) && userNames.TryGetValue(userId, out var userName)
            ? userName
            : value;

    private static string GetUserDisplayName(Volo.Abp.Identity.IdentityUser user)
    {
        var name = string.Join(" ", new[] { user.Name, user.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return string.IsNullOrWhiteSpace(name) ? user.UserName : name;
    }

    private static string? ToDisplayValue(string fieldName, string? value, string? entityType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (fieldName.EndsWith("Date", StringComparison.OrdinalIgnoreCase)
            && DateOnly.TryParse(value, out var date))
        {
            return date.ToString("dd-MM-yyyy");
        }

        if (!int.TryParse(value, out var enumValue))
        {
            return value;
        }

        return fieldName.ToLowerInvariant() switch
        {
            "holidaycategory" when Enum.IsDefined(typeof(HolidayCategory), enumValue) => ((HolidayCategory)enumValue).ToString(),
            "daytype" when Enum.IsDefined(typeof(DayType), enumValue) => ((DayType)enumValue).ToString(),
            "status" when string.Equals(entityType, "Academic Calendar", StringComparison.OrdinalIgnoreCase)
                && Enum.IsDefined(typeof(CalendarStatus), enumValue) => ((CalendarStatus)enumValue).ToString(),
            "status" when Enum.IsDefined(typeof(CalendarDayStatus), enumValue) => ((CalendarDayStatus)enumValue).ToString(),
            "shifttype" when Enum.IsDefined(typeof(SchoolShiftType), enumValue) => ((SchoolShiftType)enumValue).ToString(),
            "reschedulemode" when Enum.IsDefined(typeof(RescheduleMode), enumValue) => ((RescheduleMode)enumValue).ToString(),
            "calendarvisibility" when Enum.IsDefined(typeof(CalendarVisibility), enumValue) => ((CalendarVisibility)enumValue).ToString(),
            "impacttype" when Enum.IsDefined(typeof(CalendarImpactType), enumValue) => ((CalendarImpactType)enumValue).ToString(),
            "impactstatus" when Enum.IsDefined(typeof(CalendarImpactStatus), enumValue) => ((CalendarImpactStatus)enumValue).ToString(),
            _ => value
        };
    }

    private static AuditLogReviewNoteDto MapReviewNote(AcademicCalendarAuditReviewNote note) => new()
    {
        Id = note.Id,
        Note = note.Note,
        CreatedBy = note.CreatedByName ?? "Unknown",
        CreatedByRole = note.CreatedByRole,
        CreatedAt = note.CreatedAt
    };

    public async Task<AcademicCalendarAuditSummaryDto> GetAuditLogsSummaryAsync()
    {
        var query = await _auditLogRepository.GetQueryableAsync();
        query = query.Where(x => x.Module == "CalendarManagement" || x.Module == "EmergencyClosure");

        var now = DateTime.UtcNow;
        var startOfThisWeek = now.AddDays(-7);
        var startOfLastWeek = now.AddDays(-14);

        var items = query.ToList();

        var mostActive = items
            .Where(x => x.ChangedAt >= startOfThisWeek)
            .GroupBy(x => x.ChangedByUserId)
            .Select(g => new AcademicCalendarAuditActiveUserDto
            {
                UserId = g.Key,
                Name = g.FirstOrDefault(x => x.ActorNameSnapshot != null)?.ActorNameSnapshot ?? "Unknown User",
                Role = g.FirstOrDefault(x => x.ActorRoleSnapshot != null)?.ActorRoleSnapshot,
                ActionCount = g.Count()
            })
            .OrderByDescending(x => x.ActionCount)
            .FirstOrDefault();

        return new AcademicCalendarAuditSummaryDto
        {
            TotalAuditEvents = items.Count,
            ChangesThisWeek = items.Count(x => x.ChangedAt >= startOfThisWeek),
            ChangesLastWeek = items.Count(x => x.ChangedAt >= startOfLastWeek && x.ChangedAt < startOfThisWeek),
            // Risk classification is out of scope for this pass — no risk engine exists yet, so this
            // stays 0 rather than fabricating a number. See implementation plan for the deferred item.
            CriticalChanges = 0,
            MostActiveUser = mostActive,
            LastSyncedAt = items.OrderByDescending(x => x.ChangedAt).FirstOrDefault()?.ChangedAt ?? now
        };
    }

    // ─── Settings ───────────────────────────────────────────────────────────

    public async Task<AcademicCalendarSettingDto> GetCalendarSettingsAsync(Guid academicMediumId)
    {
        var setting = await _settingRepository.FirstOrDefaultAsync(s => s.AcademicMediumId == academicMediumId);
        if (setting == null)
        {
            // Seed a default
            var id = GuidGenerator.Create();
            setting = new AcademicCalendarSetting(id, CurrentTenant.Id, academicMediumId);
            await _settingRepository.InsertAsync(setting);
        }
        return ObjectMapper.Map<AcademicCalendarSetting, AcademicCalendarSettingDto>(setting);
    }

    public async Task<AcademicCalendarSettingDto> CreateCalendarSettingsAsync(AcademicCalendarSettingDto input)
    {
        if (!input.AcademicMediumId.HasValue)
        {
            throw new UserFriendlyException("Academic medium is required for calendar settings.");
        }

        var id = GuidGenerator.Create();
        var setting = new AcademicCalendarSetting(id, CurrentTenant.Id, input.AcademicMediumId.Value)
        {
            WeekendRule = input.WeekendRule,
            AllowClassSpecificHolidays = input.AllowClassSpecificHolidays,
            AllowDivisionSpecificHolidays = input.AllowDivisionSpecificHolidays,
            EnableHolidayCategories = input.EnableHolidayCategories,
            EnableVacationRanges = input.EnableVacationRanges,
            EnableHalfDayRules = input.EnableHalfDayRules,
            BlockTimetableOnHoliday = input.BlockTimetableOnHoliday,
            BlockTimetableOnVacation = input.BlockTimetableOnVacation,
            BlockTimetableOnEmergencyClosure = input.BlockTimetableOnEmergencyClosure,
            BlockHomeworkDueDateOnHoliday = input.BlockHomeworkDueDateOnHoliday,
            BlockHomeworkDueDateOnVacation = input.BlockHomeworkDueDateOnVacation,
            BlockExamScheduleOnHoliday = input.BlockExamScheduleOnHoliday,
            BlockExamScheduleOnVacation = input.BlockExamScheduleOnVacation,
            AllowExamOnHalfDay = input.AllowExamOnHalfDay,
            BlockLessonPlanOnHoliday = input.BlockLessonPlanOnHoliday,
            BlockLessonPlanOnVacation = input.BlockLessonPlanOnVacation,
            BlockOnlineClassOnHoliday = input.BlockOnlineClassOnHoliday,
            BlockOnlineClassOnEventDay = input.BlockOnlineClassOnEventDay,
            AutoExemptAttendanceOnHoliday = input.AutoExemptAttendanceOnHoliday,
            AutoExemptAttendanceOnVacation = input.AutoExemptAttendanceOnVacation,
            EnableEmergencyClosure = input.EnableEmergencyClosure,
            EmergencyClosureRequiresPrincipalApproval = input.EmergencyClosureRequiresPrincipalApproval,
            AutoScanImpactedActivities = input.AutoScanImpactedActivities,
            RescheduleMode = input.RescheduleMode,
            NotifyTeachersOnCalendarChange = input.NotifyTeachersOnCalendarChange,
            NotifyParentsOnCalendarChange = input.NotifyParentsOnCalendarChange,
            NotifyStudentsOnCalendarChange = input.NotifyStudentsOnCalendarChange,
            NotifyStaffOnCalendarChange = input.NotifyStaffOnCalendarChange,
            CalendarVisibility = input.CalendarVisibility,
            RequireReasonForPublishedCalendarChange = input.RequireReasonForPublishedCalendarChange
        };

        await _settingRepository.InsertAsync(setting);

        await _auditWriter.WriteAsync(
            Guid.Empty,
            CalendarAuditAction.SettingsUpdated,
            beforeJson: null,
            afterJson: System.Text.Json.JsonSerializer.Serialize(setting),
            entityType: "Calendar Settings",
            entityName: "Calendar Settings",
            reason: "Calendar settings created");

        return ObjectMapper.Map<AcademicCalendarSetting, AcademicCalendarSettingDto>(setting);
    }

    public async Task<AcademicCalendarSettingDto> UpdateCalendarSettingsAsync(Guid id, AcademicCalendarSettingDto input)
    {
        var setting = await _settingRepository.GetAsync(id);
        var oldVal = System.Text.Json.JsonSerializer.Serialize(setting);

        setting.WeekendRule = input.WeekendRule;
        setting.AllowClassSpecificHolidays = input.AllowClassSpecificHolidays;
        setting.AllowDivisionSpecificHolidays = input.AllowDivisionSpecificHolidays;
        setting.EnableHolidayCategories = input.EnableHolidayCategories;
        setting.EnableVacationRanges = input.EnableVacationRanges;
        setting.EnableHalfDayRules = input.EnableHalfDayRules;
        setting.BlockTimetableOnHoliday = input.BlockTimetableOnHoliday;
        setting.BlockTimetableOnVacation = input.BlockTimetableOnVacation;
        setting.BlockTimetableOnEmergencyClosure = input.BlockTimetableOnEmergencyClosure;
        setting.BlockHomeworkDueDateOnHoliday = input.BlockHomeworkDueDateOnHoliday;
        setting.BlockHomeworkDueDateOnVacation = input.BlockHomeworkDueDateOnVacation;
        setting.BlockExamScheduleOnHoliday = input.BlockExamScheduleOnHoliday;
        setting.BlockExamScheduleOnVacation = input.BlockExamScheduleOnVacation;
        setting.AllowExamOnHalfDay = input.AllowExamOnHalfDay;
        setting.BlockLessonPlanOnHoliday = input.BlockLessonPlanOnHoliday;
        setting.BlockLessonPlanOnVacation = input.BlockLessonPlanOnVacation;
        setting.BlockOnlineClassOnHoliday = input.BlockOnlineClassOnHoliday;
        setting.BlockOnlineClassOnEventDay = input.BlockOnlineClassOnEventDay;
        setting.AutoExemptAttendanceOnHoliday = input.AutoExemptAttendanceOnHoliday;
        setting.AutoExemptAttendanceOnVacation = input.AutoExemptAttendanceOnVacation;
        setting.EnableEmergencyClosure = input.EnableEmergencyClosure;
        setting.EmergencyClosureRequiresPrincipalApproval = input.EmergencyClosureRequiresPrincipalApproval;
        setting.AutoScanImpactedActivities = input.AutoScanImpactedActivities;
        setting.RescheduleMode = input.RescheduleMode;
        setting.NotifyTeachersOnCalendarChange = input.NotifyTeachersOnCalendarChange;
        setting.NotifyParentsOnCalendarChange = input.NotifyParentsOnCalendarChange;
        setting.NotifyStudentsOnCalendarChange = input.NotifyStudentsOnCalendarChange;
        setting.NotifyStaffOnCalendarChange = input.NotifyStaffOnCalendarChange;
        setting.CalendarVisibility = input.CalendarVisibility;
        setting.RequireReasonForPublishedCalendarChange = input.RequireReasonForPublishedCalendarChange;

        await _settingRepository.UpdateAsync(setting);

        await _auditWriter.WriteAsync(
            Guid.Empty,
            CalendarAuditAction.SettingsUpdated,
            beforeJson: oldVal,
            afterJson: System.Text.Json.JsonSerializer.Serialize(setting),
            entityType: "Calendar Settings",
            entityName: "Calendar Settings");

        return ObjectMapper.Map<AcademicCalendarSetting, AcademicCalendarSettingDto>(setting);
    }

    // ─── Dashboard statistics ────────────────────────────────────────────────

    public async Task<CalendarDashboardDto> GetCalendarDashboardAsync(Guid academicMediumId)
    {
        var calendar = await _calendarRepository.FirstOrDefaultAsync(c =>
            c.AcademicMediumId == academicMediumId && c.IsDefault);
            
        if (calendar == null)
        {
            calendar = await _calendarRepository.FirstOrDefaultAsync(c =>
                c.AcademicMediumId == academicMediumId);
        }

        var db = new CalendarDashboardDto { AcademicMediumId = academicMediumId };

        if (calendar != null)
        {
            var days = await _calendarDayRepository.GetListAsync(d => d.AcademicCalendarId == calendar.Id && d.Status == CalendarDayStatus.Active);
            
            db.TotalWorkingDays = days.Count(d => d.DayType == DayType.WorkingDay);
            db.TotalHolidays = days.Count(d => d.DayType == DayType.Holiday);
            db.TotalVacationDays = days.Count(d => d.DayType == DayType.Vacation);
            db.TotalHalfDays = days.Count(d => d.DayType == DayType.HalfDay);
            db.TotalEvents = days.Count(d => d.DayType == DayType.Event);
            db.TotalExamDays = days.Count(d => d.DayType == DayType.Exam);
            db.TotalPtmDays = days.Count(d => d.DayType == DayType.PTM);
            db.TotalEmergencyClosures = days.Count(d => d.DayType == DayType.EmergencyClosure);

            var pendingLogs = await _impactLogRepository.CountAsync(i =>
                days.Select(d => d.Id).Contains(i.AcademicCalendarDayId) && 
                i.Status == CalendarImpactStatus.Pending);
            db.PendingImpactLogsCount = (int)pendingLogs;

            var upcoming = days.Where(d => d.Date >= DateTime.Today)
                              .OrderBy(d => d.Date)
                              .Take(5)
                              .ToList();
            db.UpcomingDays = ObjectMapper.Map<List<AcademicCalendarDay>, List<AcademicCalendarDayDto>>(upcoming);
        }

        return db;
    }
}
