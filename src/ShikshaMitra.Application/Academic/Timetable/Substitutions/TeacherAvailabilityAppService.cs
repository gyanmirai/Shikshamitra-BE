using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

[RemoteService(IsEnabled = false)]
public class TeacherAvailabilityAppService(
    IRepository<TeacherAvailability, Guid> availabilityRepository,
    IRepository<SubstitutionRequest, Guid> substitutionRepository,
    IRepository<SubstitutionAuditLog, Guid> auditRepository,
    ITimetableRepository timetableRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<Room, Guid> roomRepository,
    IRepository<Employee, Guid> employeeRepository,
    IEmployeeAttendanceRepository employeeAttendanceRepository
) : ApplicationService, ITeacherAvailabilityAppService
{
    public async Task<PagedResultDto<TeacherAvailabilityDto>> GetListAsync(GetTeacherAvailabilityInput input)
    {
        await SyncAttendanceAvailabilityRowsAsync(input);

        var rows = await availabilityRepository.GetListAsync(x => !x.IsCancelled);
        var filtered = rows.AsEnumerable();

        if (!IsAdminUser())
        {
            var currentUserId = CurrentUser.Id;
            if (currentUserId.HasValue)
            {
                var currentEmployee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId.Value);
                if (currentEmployee != null)
                {
                    var allowedTeacherIds = await GetReportingEmployeesAsync(currentEmployee.Id);
                    allowedTeacherIds.Add(currentEmployee.Id);
                    filtered = filtered.Where(x => x.TeacherId.HasValue && allowedTeacherIds.Contains(x.TeacherId.Value));
                }
                else
                {
                    filtered = Enumerable.Empty<TeacherAvailability>();
                }
            }
            else
            {
                filtered = Enumerable.Empty<TeacherAvailability>();
            }
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
        var dtoRows = new List<TeacherAvailabilityDto>();
        foreach (var row in pageRows)
        {
            dtoRows.Add(await MapAsync(row));
        }

        return new PagedResultDto<TeacherAvailabilityDto>(total, dtoRows);
    }

    private async Task SyncAttendanceAvailabilityRowsAsync(GetTeacherAvailabilityInput input)
    {
        var rangeStart = input.Date ?? input.FromDate;
        var rangeEnd = input.Date ?? input.ToDate;
        if (!rangeStart.HasValue || !rangeEnd.HasValue)
        {
            return;
        }

        var from = DateOnly.FromDateTime(rangeStart.Value.Date);
        var to = DateOnly.FromDateTime(rangeEnd.Value.Date);
        var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
        var attendanceRows = await AsyncExecuter.ToListAsync(
            attendanceQuery.Where(x =>
                x.AttendanceDate >= from &&
                x.AttendanceDate <= to &&
                x.StatusType != EmployeeAttendanceStatusType.Present));

        if (attendanceRows.Count == 0)
        {
            return;
        }

        var employeeIds = attendanceRows.Select(x => x.EmployeeId).Distinct().ToList();
        var employees = await employeeRepository.GetListAsync(x => employeeIds.Contains(x.Id));
        var employeeById = employees.ToDictionary(x => x.Id);

        var availabilityStart = NormalizeDate(rangeStart.Value);
        var availabilityEnd = NormalizeDate(rangeEnd.Value);
        var existingRows = await availabilityRepository.GetListAsync(x =>
            !x.IsCancelled &&
            x.SourceType == TeacherAvailabilitySource.Attendance &&
            x.AvailabilityDate >= availabilityStart &&
            x.AvailabilityDate <= availabilityEnd);

        foreach (var attendance in attendanceRows)
        {
            if (!employeeById.TryGetValue(attendance.EmployeeId, out var employee))
            {
                continue;
            }

            var availabilityDate = NormalizeDate(attendance.AttendanceDate.ToDateTime(TimeOnly.MinValue));
            var hasExisting = existingRows.Any(x =>
                x.SourceEntityId == attendance.Id ||
                (x.TeacherId == attendance.EmployeeId && x.AvailabilityDate == availabilityDate));

            if (hasExisting)
            {
                continue;
            }

            var isFullDay = attendance.StatusType == EmployeeAttendanceStatusType.FullDayLeave;
            await CreateAsync(new CreateTeacherAvailabilityDto
            {
                TeacherId = employee.Id,
                TeacherName = string.IsNullOrWhiteSpace(employee.EmployeeName)
                    ? employee.EmployeeCode
                    : employee.EmployeeName,
                DepartmentName = employee.Department ?? "Academic",
                AvailabilityDate = availabilityDate,
                StartTime = isFullDay ? "09:00" : "09:00",
                EndTime = isFullDay ? "14:15" : "12:00",
                AvailabilityStatus = TeacherAvailabilityStatus.Unavailable,
                SourceType = TeacherAvailabilitySource.Attendance,
                SourceEntityId = attendance.Id,
                Reason = isFullDay
                    ? "Marked absent in HR attendance."
                    : "Marked half-day unavailable in HR attendance.",
                IsFullDay = isFullDay,
                AutoCreateSubstitutions = true,
                NotifyLeadership = true,
                Remarks = attendance.Remarks
            });
        }
    }

    public async Task<TeacherAvailabilityDto> CreateAsync(CreateTeacherAvailabilityDto input)
    {
        if (!IsAdminUser())
        {
            throw new UserFriendlyException("Only administrators can create unavailability.");
        }

        if (string.IsNullOrWhiteSpace(input.TeacherName) && !input.TeacherId.HasValue)
        {
            throw new UserFriendlyException("Teacher is required.");
        }

        var availabilityDate = NormalizeDate(input.AvailabilityDate);
        var affected = await GetAffectedLecturesAsync(input);
        var entity = new TeacherAvailability(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            input.TeacherName,
            availabilityDate,
            ParseTime(input.StartTime, "startTime"),
            ParseTime(input.EndTime, "endTime"),
            input.AvailabilityStatus,
            input.SourceType,
            input.Reason,
            input.IsFullDay,
            input.AutoCreateSubstitutions,
            input.NotifyLeadership,
            input.AcademicYearId,
            input.TeacherId,
            input.DepartmentName,
            input.SourceEntityId,
            affected.Items.Count,
            input.AttachmentName,
            input.Remarks);

        await availabilityRepository.InsertAsync(entity, autoSave: true);

        if (input.AutoCreateSubstitutions)
        {
            foreach (var lecture in affected.Items.Where(x => x.CanCreateSubstitution))
            {
                if (IsPastWindow(availabilityDate, ParseTime(lecture.EndTime, "endTime")))
                {
                    continue;
                }

                var duplicate = lecture.ExistingSubstitutionId.HasValue ||
                    await substitutionRepository.AnyAsync(x =>
                        x.Date == availabilityDate &&
                        x.TimetableSlotSubjectId == lecture.SlotSubjectId &&
                        x.OriginalTeacherId == lecture.TeacherId &&
                        x.Status != SubstitutionStatus.Cancelled &&
                        x.Status != SubstitutionStatus.Rejected &&
                        x.Status != SubstitutionStatus.Expired);

                if (duplicate)
                {
                    continue;
                }

                var request = new SubstitutionRequest(
                    GuidGenerator.Create(),
                    CurrentTenant.Id,
                    availabilityDate,
                    lecture.Period,
                    ParseTime(lecture.StartTime, "startTime"),
                    ParseTime(lecture.EndTime, "endTime"),
                    lecture.ClassName,
                    lecture.DivisionName,
                    lecture.SubjectName,
                    lecture.TeacherName,
                    lecture.RoomName,
                    input.Reason,
                    SubstitutionPriority.High,
                    MapAvailabilitySourceToSubstitutionSource(input.SourceType),
                    true,
                    input.AcademicYearId,
                    lecture.StandardId,
                    lecture.DivisionId,
                    lecture.TimetableId,
                    lecture.TimetableSlotId,
                    lecture.SlotSubjectId,
                    lecture.SubjectId,
                    lecture.TeacherId,
                    lecture.RoomId,
                    lecture.SubjectName,
                    input.Remarks);

                await substitutionRepository.InsertAsync(request, autoSave: true);
                await auditRepository.InsertAsync(
                    new SubstitutionAuditLog(
                        GuidGenerator.Create(),
                        CurrentTenant.Id,
                        request.Id,
                        CurrentUser.Name ?? CurrentUser.UserName ?? "System",
                        "Auto Created",
                        "Substitution created from teacher availability.",
                        CurrentUser.Id),
                    autoSave: true);
            }
        }

        return Map(entity);
    }

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
        var teacherReferenceIds = GetTeacherReferenceIds(employee, teacherId);
        var timetables = new List<Timetables.Timetable>();
        foreach (var teacherReferenceId in teacherReferenceIds)
        {
            timetables.AddRange(await timetableRepository.GetByEmployeeIdAsync(
                teacherReferenceId,
                input.AcademicYearId));
        }

        timetables = timetables
            .Where(x => x.Status == TimetableStatus.Published)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();

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
            var employee = await employeeRepository.FirstOrDefaultAsync(x =>
                x.Id == teacherId.Value || x.UserId == teacherId.Value);
            if (employee != null)
            {
                return employee;
            }
        }

        return string.IsNullOrWhiteSpace(teacherName)
            ? null
            : await employeeRepository.FirstOrDefaultAsync(x => x.EmployeeName == teacherName);
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

    public async Task<int> CreateSubstitutionsAsync(Guid id)
    {
        if (!IsAdminUser())
        {
            throw new UserFriendlyException("Only administrators can create substitutions from unavailability.");
        }

        var entity = await availabilityRepository.GetAsync(id);
        var input = new CreateTeacherAvailabilityDto
        {
            AcademicYearId = entity.AcademicYearId,
            TeacherId = entity.TeacherId,
            TeacherName = entity.TeacherName,
            DepartmentName = entity.DepartmentName,
            AvailabilityDate = entity.AvailabilityDate,
            StartTime = entity.StartTime.ToString("HH:mm"),
            EndTime = entity.EndTime.ToString("HH:mm"),
            AvailabilityStatus = entity.AvailabilityStatus,
            SourceType = entity.SourceType,
            SourceEntityId = entity.SourceEntityId,
            Reason = entity.Reason,
            IsFullDay = entity.IsFullDay,
            AutoCreateSubstitutions = false,
            NotifyLeadership = entity.NotifyLeadership,
            AttachmentName = entity.AttachmentName,
            Remarks = entity.Remarks
        };
        var affected = await GetAffectedLecturesAsync(input);
        var created = 0;
        foreach (var lecture in affected.Items.Where(x => x.CanCreateSubstitution))
        {
            if (IsPastWindow(entity.AvailabilityDate, ParseTime(lecture.EndTime, "endTime")))
            {
                continue;
            }

            var request = new SubstitutionRequest(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                entity.AvailabilityDate,
                lecture.Period,
                ParseTime(lecture.StartTime, "startTime"),
                ParseTime(lecture.EndTime, "endTime"),
                lecture.ClassName,
                lecture.DivisionName,
                lecture.SubjectName,
                lecture.TeacherName,
                lecture.RoomName,
                entity.Reason,
                SubstitutionPriority.High,
                MapAvailabilitySourceToSubstitutionSource(entity.SourceType),
                true,
                entity.AcademicYearId,
                lecture.StandardId,
                lecture.DivisionId,
                lecture.TimetableId,
                lecture.TimetableSlotId,
                lecture.SlotSubjectId,
                lecture.SubjectId,
                lecture.TeacherId,
                lecture.RoomId,
                lecture.SubjectName,
                entity.Remarks);
            await substitutionRepository.InsertAsync(request, autoSave: true);
            created++;
        }

        return created;
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
        var existingRow = existing.FirstOrDefault(x => x.TimetableSlotSubjectId == slotSubject.Id);

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
            CanCreateSubstitution = existingRow == null && !IsPastWindow(availabilityDate, slot.EndTime)
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

    private async Task<TeacherAvailabilityDto> MapAsync(TeacherAvailability row)
    {
        var dto = Map(row);
        if (row.AffectedLectureCount <= 0)
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
            StartTime = row.StartTime.ToString("HH:mm"),
            EndTime = row.EndTime.ToString("HH:mm"),
            AvailabilityStatus = row.AvailabilityStatus,
            SourceType = row.SourceType,
            SourceEntityId = row.SourceEntityId,
            Reason = row.Reason,
            IsFullDay = row.IsFullDay,
            AutoCreateSubstitutions = false,
            NotifyLeadership = row.NotifyLeadership,
            AttachmentName = row.AttachmentName,
            Remarks = row.Remarks
        });
        dto.CanCreateSubstitution = affected.Items.Any(x => x.CanCreateSubstitution);
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
        AutoCreateSubstitutions = row.AutoCreateSubstitutions,
        NotifyLeadership = row.NotifyLeadership,
        CanCreateSubstitution = row.AffectedLectureCount > 0,
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

    private static DateTime CombineDateAndTime(DateTime date, TimeOnly time) =>
        DateTime.SpecifyKind(date.Date.Add(time.ToTimeSpan()), DateTimeKind.Utc);

    private static bool IsPastWindow(DateTime date, TimeOnly endTime) =>
        DateTime.UtcNow > CombineDateAndTime(date, endTime);

    private static SubstitutionSource MapAvailabilitySourceToSubstitutionSource(
        TeacherAvailabilitySource sourceType) => sourceType switch
    {
        TeacherAvailabilitySource.HrLeave => SubstitutionSource.HrLeave,
        TeacherAvailabilitySource.Attendance => SubstitutionSource.Attendance,
        TeacherAvailabilitySource.AcademicCalendar => SubstitutionSource.AcademicCalendar,
        TeacherAvailabilitySource.ExamDuty => SubstitutionSource.ExamDuty,
        _ => SubstitutionSource.AutoCreated
    };

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

    private async Task<List<Guid>> GetReportingEmployeesAsync(Guid managerEmployeeId)
    {
        var allEmployees = await employeeRepository.GetListAsync();
        var result = new List<Guid>();
        var visited = new HashSet<Guid>();
        
        void Traverse(Guid parentId)
        {
            if (!visited.Add(parentId))
            {
                return;
            }
            var children = allEmployees.Where(x => x.ReportingManagerId == parentId).ToList();
            foreach (var child in children)
            {
                result.Add(child.Id);
                Traverse(child.Id);
            }
        }
        
        Traverse(managerEmployeeId);
        return result;
    }
}
