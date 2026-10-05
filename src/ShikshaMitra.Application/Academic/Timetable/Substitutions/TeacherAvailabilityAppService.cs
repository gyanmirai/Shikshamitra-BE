using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.HR.AttendanceAndLeaves;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

[RemoteService(IsEnabled = false)]
public class TeacherAvailabilityAppService(
    IRepository<TeacherAvailability, Guid> availabilityRepository,
    IRepository<SubstitutionRequest, Guid> substitutionRepository,
    ITimetableRepository timetableRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<Room, Guid> roomRepository,
    IRepository<Employee, Guid> employeeRepository,
    AttendanceAvailabilityWindowResolver attendanceWindowResolver,
    IRepository<AcademicYear, Guid> academicYearRepository,
    IUserScopeAccessService userScopeAccessService,
    IReportingManagerAccessService reportingManagerAccessService
) : ApplicationService, ITeacherAvailabilityAppService
{
    private const string AvailabilityOverlapMessage =
        "Teacher is already marked unavailable for the selected date and time. Please choose a different time slot.";

    public async Task<PagedResultDto<TeacherAvailabilityDto>> GetListAsync(GetTeacherAvailabilityInput input)
    {
        var visibility = IsAdminUser()
            ? null
            : await GetVisibleTeachersAsync();

        var rows = await availabilityRepository.GetListAsync(x => !x.IsCancelled);
        var filtered = rows.AsEnumerable();

        if (visibility != null)
        {
            filtered = filtered.Where(x =>
                x.TeacherId.HasValue && visibility.ReferenceIds.Contains(x.TeacherId.Value));
        }

        if (input.AcademicYearId.HasValue) filtered = filtered.Where(x => x.AcademicYearId == input.AcademicYearId);
        if (input.Date.HasValue) filtered = filtered.Where(x => x.AvailabilityDate == NormalizeDate(input.Date.Value));
        if (input.FromDate.HasValue) filtered = filtered.Where(x => x.AvailabilityDate >= NormalizeDate(input.FromDate.Value));
        if (input.ToDate.HasValue) filtered = filtered.Where(x => x.AvailabilityDate <= NormalizeDate(input.ToDate.Value));
        if (input.TeacherId.HasValue) filtered = filtered.Where(x => x.TeacherId == input.TeacherId);
        if (!string.IsNullOrWhiteSpace(input.TeacherName))
        {
            filtered = filtered.Where(x => string.Equals(x.TeacherName, input.TeacherName, StringComparison.OrdinalIgnoreCase));
        }
        if (input.SourceType.HasValue) filtered = filtered.Where(x => x.SourceType == input.SourceType);

        var list = filtered.OrderBy(x => x.AvailabilityDate).ThenBy(x => x.StartTime).ToList();
        var total = list.Count;
        var max = input.MaxResultCount <= 0 ? 50 : input.MaxResultCount;

        var pageRows = list.Skip(input.SkipCount).Take(max).ToList();
        var attendanceRows = pageRows.Where(x => x.SourceType == TeacherAvailabilitySource.Attendance).ToList();
        var teacherIds = attendanceRows.Where(x => x.TeacherId.HasValue).Select(x => x.TeacherId!.Value).Distinct().ToArray();
        var teacherNames = attendanceRows.Where(x => !x.TeacherId.HasValue).Select(x => x.TeacherName).Distinct().ToArray();
        var employees = teacherIds.Length == 0 ? [] : await employeeRepository.GetListAsync(x =>
            Enumerable.Contains(teacherIds, x.Id) ||
            (x.UserId.HasValue && Enumerable.Contains(teacherIds, x.UserId.Value)));
        if (teacherNames.Length > 0)
        {
            // EmployeeName is computed and cannot be used in an EF predicate.
            employees.AddRange((await employeeRepository.GetListAsync()).Where(x =>
                teacherNames.Contains(x.EmployeeName, StringComparer.OrdinalIgnoreCase) &&
                employees.All(existing => existing.Id != x.Id)));
        }
        var shifts = await attendanceWindowResolver.LoadShiftsAsync(employees);
        var dtoRows = new List<TeacherAvailabilityDto>();
        foreach (var row in pageRows)
        {
            var employee = ResolveEmployeeByTeacherReference(employees, row.TeacherId, row.TeacherName);
            var shift = employee?.ShiftId is Guid shiftId ? shifts.GetValueOrDefault(shiftId) : null;
            var window = row.SourceType == TeacherAvailabilitySource.Attendance
                ? AttendanceAvailabilityWindowResolver.Resolve(shift, row.IsFullDay, row.StartTime, row.EndTime)
                : (row.StartTime, row.EndTime);
            dtoRows.Add(await MapAsync(row, window));
        }

        return new PagedResultDto<TeacherAvailabilityDto>(total, dtoRows);
    }

    public async Task<TeacherAvailabilityDto> CreateAsync(CreateTeacherAvailabilityDto input)
    {
        if (!IsAdminUser())
        {
            throw new UserFriendlyException("Only administrators can create unavailability.");
        }

        return await CreateAvailabilityAsync(input);
    }

    private async Task<TeacherAvailabilityDto> CreateAvailabilityAsync(
        CreateTeacherAvailabilityDto input,
        bool skipIfOverlapping = false)
    {
        if (string.IsNullOrWhiteSpace(input.TeacherName) && !input.TeacherId.HasValue)
        {
            throw new UserFriendlyException("Teacher is required.");
        }

        var availabilityDate = NormalizeDate(input.AvailabilityDate);
        var startTime = ParseTime(input.StartTime, "startTime");
        var endTime = ParseTime(input.EndTime, "endTime");
        if (endTime <= startTime)
        {
            throw new UserFriendlyException("End time must be later than start time.");
        }

        var existingRows = await availabilityRepository.GetListAsync(x =>
            !x.IsCancelled && x.AvailabilityDate == availabilityDate);
        var teacherName = input.TeacherName?.Trim() ?? string.Empty;
        var overlappingAvailability = existingRows.FirstOrDefault(existing =>
            IsSameTeacher(existing, input.TeacherId, teacherName) &&
            existing.StartTime < endTime &&
            startTime < existing.EndTime);

        if (overlappingAvailability != null)
        {
            if (skipIfOverlapping)
            {
                return Map(overlappingAvailability);
            }

            throw new UserFriendlyException(AvailabilityOverlapMessage);
        }

        var academicYearId = input.AcademicYearId ?? await ResolveAcademicYearIdAsync(availabilityDate);

        var affected = await GetAffectedLecturesAsync(input);
        var entity = new TeacherAvailability(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.TeacherName,
            availabilityDate,
            startTime,
            endTime,
            input.AvailabilityStatus,
            input.SourceType,
            input.Reason,
            input.IsFullDay,
            input.NotifyLeadership,
            academicYearId,
            input.TeacherId,
            input.DepartmentName,
            input.SourceEntityId,
            affected.Items.Count,
            input.AttachmentName,
            input.Remarks);

        await availabilityRepository.InsertAsync(entity, autoSave: true);

        return Map(entity);
    }

    private async Task<Guid?> ResolveAcademicYearIdAsync(DateTime availabilityDate)
    {
        var query = await academicYearRepository.GetQueryableAsync();
        var academicYear = await AsyncExecuter.FirstOrDefaultAsync(
            query
                .Where(year => year.StartDate.Date <= availabilityDate.Date &&
                               year.EndDate.Date >= availabilityDate.Date)
                .OrderByDescending(year => year.StartDate));

        return academicYear?.Id;
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<AffectedLectureResultDto> GetAffectedLecturesAsync(CreateTeacherAvailabilityDto input)
    {
        var teacherId = input.TeacherId;
        var employee = await FindTeacherEmployeeAsync(teacherId, input.TeacherName);
        if (!teacherId.HasValue)
        {
            teacherId = employee?.Id;
        }

        if (!teacherId.HasValue)
        {
            return new AffectedLectureResultDto();
        }

        var availabilityDate = NormalizeDate(input.AvailabilityDate);
        var timetableDay = ToTimetableDay(availabilityDate.DayOfWeek);
        if (!timetableDay.HasValue)
        {
            return new AffectedLectureResultDto();
        }

        var start = ParseTime(input.StartTime, "startTime");
        var end = ParseTime(input.EndTime, "endTime");
        var academicYearId = input.AcademicYearId ?? await ResolveAcademicYearIdAsync(availabilityDate);
        var teacherReferenceIds = GetTeacherReferenceIds(employee, teacherId);
        var timetables = new List<Timetables.Timetable>();
        foreach (var teacherReferenceId in teacherReferenceIds)
        {
            timetables.AddRange(await timetableRepository.GetByEmployeeIdAsync(
                teacherReferenceId,
                academicYearId));
        }

        timetables = timetables
            .Where(x => x.Status == TimetableStatus.Published)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();

        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        timetables = timetables.FilterByUserScope(scope).ToList();

        if (input.AcademicMediumId.HasValue)
        {
            var scopedStandardIds = await AsyncExecuter.ToListAsync(
                (await standardRepository.GetQueryableAsync())
                    .Where(standard => standard.EducationMediumStage.EducationMediumId == input.AcademicMediumId.Value)
                    .Select(standard => standard.Id));
            var allowedStandardIds = scopedStandardIds.ToHashSet();
            timetables = timetables.Where(timetable => allowedStandardIds.Contains(timetable.StandardId)).ToList();
        }

        var lectureRows = timetables
            .SelectMany(timetable => timetable.Slots
                .Where(slot => slot.Day == timetableDay.Value && slot.StartTime < end && slot.EndTime > start)
                .SelectMany(slot => slot.Subjects
                    .Where(subject => subject.TeacherId.HasValue && teacherReferenceIds.Contains(subject.TeacherId.Value))
                    .Select(subject => new
                    {
                        Timetable = timetable,
                        Slot = slot,
                        Subject = subject
                    })))
            .ToList();

        if (lectureRows.Count == 0)
        {
            return new AffectedLectureResultDto();
        }

        var standardIds = lectureRows.Select(x => x.Timetable.StandardId).Distinct().ToList();
        var divisionIds = lectureRows.Select(x => x.Timetable.DivisionId).Distinct().ToList();
        var subjectIds = lectureRows.Select(x => x.Subject.SubjectId).Distinct().ToList();
        var roomIds = lectureRows
            .Where(x => x.Subject.RoomId.HasValue)
            .Select(x => x.Subject.RoomId!.Value)
            .Distinct()
            .ToList();
        var standards = await standardRepository.GetListAsync(x => standardIds.Contains(x.Id));
        var divisions = await divisionRepository.GetListAsync(x => divisionIds.Contains(x.Id));
        var subjects = await subjectRepository.GetListAsync(x => subjectIds.Contains(x.Id));
        var rooms = roomIds.Count == 0
            ? []
            : await roomRepository.GetListAsync(x => roomIds.Contains(x.Id));
        var employees = employee == null ? [] : new List<Employee> { employee };
        var existing = await substitutionRepository.GetListAsync(x =>
            x.Date == availabilityDate &&
            x.OriginalTeacherId.HasValue &&
            teacherReferenceIds.Contains(x.OriginalTeacherId.Value) &&
            x.Status != SubstitutionStatus.Cancelled &&
            x.Status != SubstitutionStatus.Rejected &&
            x.Status != SubstitutionStatus.Expired);

        var items = lectureRows
            .Select(row => BuildAffectedLecture(
                row.Timetable,
                row.Slot,
                row.Subject,
                standards,
                divisions,
                subjects,
                rooms,
                employees,
                existing,
                availabilityDate))
            .OrderBy(x => x.StartTime)
            .ToList();

        return new AffectedLectureResultDto { Items = items };
    }

    private async Task<Employee?> FindTeacherEmployeeAsync(Guid? teacherId, string? teacherName)
    {
        if (teacherId.HasValue)
        {
            return await employeeRepository.FirstOrDefaultAsync(x =>
                x.Id == teacherId.Value || x.UserId == teacherId.Value);
        }

        return string.IsNullOrWhiteSpace(teacherName)
            ? null
            : (await employeeRepository.GetListAsync()).FirstOrDefault(x =>
                string.Equals(x.EmployeeName, teacherName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task CancelAsync(Guid id)
    {
        if (!IsAdminUser())
        {
            throw new UserFriendlyException("Only administrators can cancel unavailability.");
        }

        var entity = await availabilityRepository.GetAsync(id);
        entity.Cancel();
        await availabilityRepository.UpdateAsync(entity, autoSave: true);
    }

    private static AffectedLectureDto BuildAffectedLecture(
        Timetables.Timetable timetable,
        TimetableSlot slot,
        TimetableSlotSubject slotSubject,
        List<Standard> standards,
        List<Division> divisions,
        List<Subject> subjects,
        List<Room> rooms,
        List<Employee> employees,
        List<SubstitutionRequest> existing,
        DateTime availabilityDate)
    {
        var subject = subjects.FirstOrDefault(x => x.Id == slotSubject.SubjectId);
        var standard = standards.FirstOrDefault(x => x.Id == timetable.StandardId);
        var division = divisions.FirstOrDefault(x => x.Id == timetable.DivisionId);
        var room = slotSubject.RoomId.HasValue ? rooms.FirstOrDefault(x => x.Id == slotSubject.RoomId.Value) : null;
        var teacher = ResolveEmployeeByTeacherReference(employees, slotSubject.TeacherId, null);
        var existingRow = existing.FirstOrDefault(x =>
            x.TimetableSlotSubjectId == slotSubject.Id ||
            (x.TimetableSlotSubjectId == null &&
             x.TimetableId == timetable.Id &&
             x.TimetableSlotId == slot.Id &&
             x.SubjectId == slotSubject.SubjectId &&
             x.StartTime < slot.EndTime &&
             x.EndTime > slot.StartTime));

        var isPastWindow = IsPastWindow(availabilityDate, slot.EndTime);
        return new AffectedLectureDto
        {
            TimetableId = timetable.Id,
            TimetableSlotId = slot.Id,
            SlotSubjectId = slotSubject.Id,
            Period = $"Period {slot.Sequence}",
            StartTime = slot.StartTime.ToString("HH:mm"),
            EndTime = slot.EndTime.ToString("HH:mm"),
            Time = $"{slot.StartTime:HH:mm} - {slot.EndTime:HH:mm}",
            StandardId = timetable.StandardId,
            ClassName = standard?.Name ?? string.Empty,
            DivisionId = timetable.DivisionId,
            DivisionName = division?.Name ?? string.Empty,
            SubjectId = slotSubject.SubjectId,
            SubjectName = subject?.Name ?? string.Empty,
            RoomId = slotSubject.RoomId,
            RoomName = room?.RoomName ?? string.Empty,
            TeacherId = teacher?.Id ?? slotSubject.TeacherId,
            TeacherName = teacher?.EmployeeName ?? string.Empty,
            ExistingSubstitutionId = existingRow?.Id,
            ExistingSubstitutionStatus = existingRow?.Status.ToString(),
            CanCreateSubstitution = existingRow == null && !isPastWindow,
            CreateSubstitutionBlockReason = existingRow != null
                ? "Already assigned"
                : isPastWindow ? "Lecture ended" : null
        };
    }

    private static Employee? ResolveEmployeeByTeacherReference(
        List<Employee> employees,
        Guid? teacherId,
        string? teacherName)
    {
        if (teacherId.HasValue)
        {
            var byId = employees.FirstOrDefault(x => x.Id == teacherId.Value);
            if (byId != null)
            {
                return byId;
            }

            var byUserId = employees.FirstOrDefault(x => x.UserId == teacherId.Value);
            if (byUserId != null)
            {
                return byUserId;
            }
        }

        return string.IsNullOrWhiteSpace(teacherName)
            ? null
            : employees.FirstOrDefault(x =>
                string.Equals(x.EmployeeName, teacherName, StringComparison.OrdinalIgnoreCase));
    }

    private static List<Guid> GetTeacherReferenceIds(Employee? employee, Guid? fallbackId)
    {
        var ids = new List<Guid>();
        if (employee != null)
        {
            ids.Add(employee.Id);
            if (employee.UserId.HasValue)
            {
                ids.Add(employee.UserId.Value);
            }
        }
        else if (fallbackId.HasValue)
        {
            ids.Add(fallbackId.Value);
        }

        return ids.Distinct().ToList();
    }

    private async Task<TeacherAvailabilityDto> MapAsync(TeacherAvailability row, (TimeOnly Start, TimeOnly End) window)
    {
        var dto = Map(row);
        dto.StartTime = window.Start.ToString("HH:mm");
        dto.EndTime = window.End.ToString("HH:mm");

        if (row.AffectedLectureCount <= 0 && row.SourceType != TeacherAvailabilitySource.Attendance)
        {
            dto.CanCreateSubstitution = false;
            return dto;
        }

        var affected = await GetAffectedLecturesAsync(new CreateTeacherAvailabilityDto
        {
            AcademicYearId = row.AcademicYearId,
            TeacherId = row.TeacherId,
            TeacherName = row.TeacherName,
            DepartmentName = row.DepartmentName,
            AvailabilityDate = row.AvailabilityDate,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            AvailabilityStatus = row.AvailabilityStatus,
            SourceType = row.SourceType,
            SourceEntityId = row.SourceEntityId,
            Reason = row.Reason,
            IsFullDay = row.IsFullDay,
            NotifyLeadership = row.NotifyLeadership,
            AttachmentName = row.AttachmentName,
            Remarks = row.Remarks
        });
        TeacherAvailabilityCoverage.Apply(dto, affected.Items);
        return dto;
    }

    private static TeacherAvailabilityDto Map(TeacherAvailability row) => new()
    {
        Id = row.Id,
        TeacherId = row.TeacherId,
        TeacherName = row.TeacherName,
        DepartmentName = row.DepartmentName,
        AvailabilityDate = row.AvailabilityDate,
        StartTime = row.StartTime.ToString("HH:mm"),
        EndTime = row.EndTime.ToString("HH:mm"),
        AvailabilityStatus = row.AvailabilityStatus.ToString(),
        SourceType = row.SourceType.ToString(),
        Reason = row.Reason,
        IsFullDay = row.IsFullDay,
        AffectedLectureCount = row.AffectedLectureCount,
        CoveredLectureCount = 0,
        UncoveredLectureCount = row.AffectedLectureCount,
        NotifyLeadership = row.NotifyLeadership,
        CanCreateSubstitution = row.AffectedLectureCount > 0,
        ActiveSubstitutionStatuses = [],
        ActiveSubstitutionIds = [],
        PendingSubstitutionIds = [],
        AttachmentName = row.AttachmentName,
        Remarks = row.Remarks
    };

    private static TimeOnly ParseTime(string value, string fieldName)
    {
        if (!TimeOnly.TryParse(value, out var parsed))
        {
            throw new UserFriendlyException($"{fieldName} is invalid.");
        }

        return parsed;
    }

    private static bool IsSameTeacher(
        TeacherAvailability existing,
        Guid? teacherId,
        string teacherName)
    {
        if (teacherId.HasValue && existing.TeacherId.HasValue)
        {
            return existing.TeacherId == teacherId;
        }

        return !string.IsNullOrWhiteSpace(teacherName) &&
            string.Equals(existing.TeacherName, teacherName, StringComparison.OrdinalIgnoreCase);
    }

    private static TimeZoneInfo GetInstitutionTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        var tz = GetInstitutionTimeZone();
        var utcTime = value.Kind == DateTimeKind.Utc ? value : TimeZoneInfo.ConvertTimeToUtc(value);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
        return DateTime.SpecifyKind(localTime.Date, DateTimeKind.Utc);
    }

    private static bool IsPastWindow(DateTime date, TimeOnly endTime)
        => !SubstitutionTimeWindow.IsOpen(date, endTime, DateTime.UtcNow, GetInstitutionTimeZone());

    private static TimetableDayOfWeek? ToTimetableDay(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => TimetableDayOfWeek.Monday,
        DayOfWeek.Tuesday => TimetableDayOfWeek.Tuesday,
        DayOfWeek.Wednesday => TimetableDayOfWeek.Wednesday,
        DayOfWeek.Thursday => TimetableDayOfWeek.Thursday,
        DayOfWeek.Friday => TimetableDayOfWeek.Friday,
        DayOfWeek.Saturday => TimetableDayOfWeek.Saturday,
        _ => null
    };

    private bool IsAdminUser()
    {
        return CurrentUser.Roles.Any(role =>
            string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<TeacherVisibility> GetVisibleTeachersAsync()
    {
        var referenceIds = new HashSet<Guid>();
        var employeeIds = new HashSet<Guid>();
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        if (scope.HasAnyAccess)
        {
            var timetables = await timetableRepository.GetAllWithSlotsAsync();
            var scopedTimetables = timetables.FilterByUserScope(scope);
            referenceIds.UnionWith(scopedTimetables
                .SelectMany(timetable => timetable.Slots)
                .SelectMany(slot => slot.Subjects)
                .Where(subject => subject.TeacherId.HasValue)
                .Select(subject => subject.TeacherId!.Value));
        }

        if (CurrentUser.Id.HasValue)
        {
            var currentUserId = CurrentUser.Id.Value;
            var reporteeIds = await reportingManagerAccessService
                .GetReporteeEmployeeIdsAsync(currentUserId);
            employeeIds.UnionWith(reporteeIds);
            referenceIds.UnionWith(reporteeIds);
        }

        var candidateIds = referenceIds.Concat(employeeIds).Distinct().ToArray();
        if (candidateIds.Length == 0)
        {
            return new TeacherVisibility(referenceIds, employeeIds);
        }

        var employees = await employeeRepository.GetListAsync(employee =>
            Enumerable.Contains(candidateIds, employee.Id) ||
            (employee.UserId.HasValue && Enumerable.Contains(candidateIds, employee.UserId.Value)));
        foreach (var employee in employees)
        {
            employeeIds.Add(employee.Id);
            referenceIds.Add(employee.Id);
            if (employee.UserId.HasValue)
            {
                referenceIds.Add(employee.UserId.Value);
            }
        }

        return new TeacherVisibility(referenceIds, employeeIds);
    }

    private sealed record TeacherVisibility(
        HashSet<Guid> ReferenceIds,
        HashSet<Guid> EmployeeIds);
}
