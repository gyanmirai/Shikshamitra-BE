using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

[RemoteService(IsEnabled = false)]
public class SubstitutionAppService(
    IRepository<SubstitutionRequest, Guid> substitutionRepository,
    IRepository<SubstitutionAuditLog, Guid> auditRepository,
    IRepository<TeacherAvailability, Guid> availabilityRepository,
    IRepository<SubstitutionNotificationLog, Guid> notificationRepository,
    IRepository<SubstitutionSetting, Guid> settingRepository,
    ITimetableRepository timetableRepository,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<Room, Guid> roomRepository,
    IRepository<Employee, Guid> employeeRepository,
    TakeAttendanceTeacherAvailabilitySyncService teacherAvailabilitySyncService
) : ApplicationService, ISubstitutionAppService
{
    public async Task<PagedResultDto<SubstitutionDto>> GetListAsync(GetSubstitutionsInput input)
    {
        var rows = await substitutionRepository.GetListAsync();
        rows = await FilterByAcademicMediumAsync(rows, input.AcademicMediumId);

        if (!IsAdminUser())
        {
            var currentUserId = CurrentUser.Id;
            if (currentUserId.HasValue)
            {
                var visibleTeacherIds = await GetVisibleTeacherReferenceIdsAsync(currentUserId.Value);
                rows = rows.Where(x =>
                    (x.OriginalTeacherId.HasValue && visibleTeacherIds.Contains(x.OriginalTeacherId.Value)) ||
                    (x.SubstituteTeacherId.HasValue && visibleTeacherIds.Contains(x.SubstituteTeacherId.Value))).ToList();
            }
            else
            {
                rows = new List<SubstitutionRequest>();
            }
        }

        var filtered = ApplyFilters(rows, input)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartTime)
            .ToList();

        var total = filtered.Count;
        var max = input.MaxResultCount <= 0 ? 50 : input.MaxResultCount;
        var paged = filtered.Skip(input.SkipCount).Take(max).ToList();
        var audits = await GetAuditLookupAsync(paged.Select(x => x.Id).ToList());
        var dtoRows = new List<SubstitutionDto>();
        foreach (var row in paged)
        {
            dtoRows.Add(await MapAsync(row, audits.GetValueOrDefault(row.Id) ?? []));
        }

        return new PagedResultDto<SubstitutionDto>(total, dtoRows);
    }

    public async Task<SubstitutionDashboardDto> GetDashboardAsync(DateTimeOffset date, Guid? academicYearId = null, Guid? academicMediumId = null)
    {
        var target = NormalizeDate(date);
        await teacherAvailabilitySyncService.SyncDateRangeAsync(target, target);

        var substitutions = await substitutionRepository.GetListAsync(x =>
            x.Date == target && (!academicYearId.HasValue || x.AcademicYearId == academicYearId));
        substitutions = await FilterByAcademicMediumAsync(substitutions, academicMediumId);

        if (!IsAdminUser())
        {
            var currentUserId = CurrentUser.Id;
            if (currentUserId.HasValue)
            {
                var visibleTeacherIds = await GetVisibleTeacherReferenceIdsAsync(currentUserId.Value);
                substitutions = substitutions.Where(x =>
                    (x.OriginalTeacherId.HasValue && visibleTeacherIds.Contains(x.OriginalTeacherId.Value)) ||
                    (x.SubstituteTeacherId.HasValue && visibleTeacherIds.Contains(x.SubstituteTeacherId.Value))).ToList();
            }
            else
            {
                substitutions = new List<SubstitutionRequest>();
            }
        }

        var availability = await availabilityRepository.GetListAsync(x =>
            x.AvailabilityDate == target &&
            !x.IsCancelled &&
            (!academicYearId.HasValue || x.AcademicYearId == academicYearId));
        if (academicMediumId.HasValue && academicMediumId.Value != Guid.Empty)
        {
            var contextTeacherUserIds = await GetTeacherUserIdsForAcademicMediumAsync(academicMediumId);
            availability = availability
                .Where(x =>
                    (x.TeacherId.HasValue && contextTeacherUserIds.Contains(x.TeacherId.Value)) ||
                    (!x.TeacherId.HasValue && !string.IsNullOrWhiteSpace(x.TeacherName) &&
                     substitutions.Any(substitution =>
                         string.Equals(substitution.OriginalTeacherName, x.TeacherName, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(substitution.SubstituteTeacherName, x.TeacherName, StringComparison.OrdinalIgnoreCase))))
                .ToList();
        }

        var nextRisk = substitutions
            .Where(x => x.Status is SubstitutionStatus.Pending or SubstitutionStatus.Conflict or SubstitutionStatus.WaitingAck)
            .OrderBy(x => x.StartTime)
            .FirstOrDefault();

        return new SubstitutionDashboardDto
        {
            Pending = substitutions.Count(x => x.Status == SubstitutionStatus.Pending),
            Assigned = substitutions.Count(x =>
                x.Status == SubstitutionStatus.Assigned ||
                x.Status == SubstitutionStatus.WaitingAck ||
                x.Status == SubstitutionStatus.Acknowledged),
            WaitingAck = substitutions.Count(x => x.Status == SubstitutionStatus.WaitingAck),
            Acknowledged = substitutions.Count(x => x.Status == SubstitutionStatus.Acknowledged),
            Completed = substitutions.Count(x => x.Status == SubstitutionStatus.Completed),
            Conflict = substitutions.Count(x => x.Status == SubstitutionStatus.Conflict),
            TeacherUnavailable = availability.Count,
            Uncovered = substitutions.Count(x => x.Status is SubstitutionStatus.Pending or SubstitutionStatus.Conflict),
            ChangeRequested = substitutions.Count(x =>
                x.AcknowledgementStatus == SubstitutionAcknowledgementStatus.ChangeRequested),
            NextRisk = nextRisk == null ? null : await MapAsync(nextRisk, await GetAuditLogsAsync(nextRisk.Id)),
            RiskAlerts = BuildRiskAlerts(substitutions),
            WorkloadSnapshot = await BuildWorkloadSnapshotAsync(target, academicMediumId)
        };
    }

    public async Task<SubstitutionDto> GetAsync(Guid id)
    {
        var entity = await substitutionRepository.GetAsync(id);
        await EnsureCanViewSubstitutionAsync(entity);
        return await MapAsync(entity, await GetAuditLogsAsync(id));
    }

    public async Task<SubstitutionDto> CreateAsync(CreateSubstitutionDto input)
    {
        await EnsureAdminAsync("create substitutions");
        var date = NormalizeDate(input.Date);
        var start = ParseTime(input.StartTime, "startTime");
        var end = ParseTime(input.EndTime, "endTime");
        if (end <= start)
        {
            throw new UserFriendlyException("End time must be after start time.");
        }
        EnsureSubstitutionWindowNotFinished(date, end, "Cannot create a substitution after the period has ended.");

        var employees = await employeeRepository.GetListAsync();
        var originalTeacher = ResolveEmployeeByTeacherReference(
            employees,
            input.OriginalTeacherId,
            input.OriginalTeacherName);
        var originalTeacherId = originalTeacher?.Id ?? input.OriginalTeacherId;
        var originalTeacherName = string.IsNullOrWhiteSpace(input.OriginalTeacherName)
            ? originalTeacher?.EmployeeName ?? string.Empty
            : input.OriginalTeacherName;

        if (input.TimetableSlotSubjectId.HasValue || originalTeacherId.HasValue)
        {
            var teacherReferenceIds = GetTeacherReferenceIds(originalTeacher, originalTeacherId);
            var duplicate = await substitutionRepository.FirstOrDefaultAsync(x =>
                x.Date == date &&
                x.TimetableSlotSubjectId == input.TimetableSlotSubjectId &&
                x.OriginalTeacherId.HasValue &&
                teacherReferenceIds.Contains(x.OriginalTeacherId.Value) &&
                x.Status != SubstitutionStatus.Cancelled &&
                x.Status != SubstitutionStatus.Rejected &&
                x.Status != SubstitutionStatus.Expired &&
                x.StartTime < end &&
                x.EndTime > start);
            if (duplicate != null)
            {
                throw new UserFriendlyException("This slot time already has an active substitution assignment.");
            }
        }

        var entity = new SubstitutionRequest(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            date,
            input.Period,
            start,
            end,
            input.ClassName ?? string.Empty,
            input.DivisionName ?? string.Empty,
            input.SubjectName ?? string.Empty,
            originalTeacherName,
            input.RoomName ?? string.Empty,
            input.Reason,
            input.Priority,
            input.Source,
            input.AcknowledgementRequired,
            input.AcademicYearId,
            input.StandardId,
            input.DivisionId,
            input.TimetableId,
            input.TimetableSlotId,
            input.TimetableSlotSubjectId,
            input.SubjectId,
            originalTeacherId,
            input.OriginalRoomId,
            input.SubjectName,
            input.Remarks);

        await substitutionRepository.InsertAsync(entity, autoSave: true);
        await WriteAuditAsync(entity.Id, "Created", "Substitution request created.");

        if (input.SubstituteTeacherId.HasValue || !string.IsNullOrWhiteSpace(input.SubstituteTeacherName))
        {
            var selected = await EnsureSubstituteCanBeAssignedAsync(
                date,
                start,
                end,
                input.StandardId,
                input.SubjectId,
                originalTeacherId,
                originalTeacherName,
                input.SubjectName,
                input.SubstituteTeacherId,
                input.SubstituteTeacherName,
                null,
                input.AcademicMediumId);
            await EnsureAssignedRoomAvailableAsync(
                date,
                start,
                end,
                input.AssignedRoomId ?? input.OriginalRoomId,
                input.TimetableSlotId,
                entity.Id);

            entity.Assign(
                input.SubstituteTeacherId ?? selected.TeacherId,
                string.IsNullOrWhiteSpace(input.SubstituteTeacherName) ? selected.TeacherName : input.SubstituteTeacherName,
                input.AssignedRoomId,
                input.AssignedRoomName,
                selected.Score,
                selected.LoadStatus,
                ParseConflict(selected.ConflictSeverity),
                selected.ConflictReason,
                input.SendNotification);
            entity.MarkNotificationSent();
            await substitutionRepository.UpdateAsync(entity, autoSave: true);
            await WriteAuditAsync(entity.Id, "Assigned", $"Assigned to {input.SubstituteTeacherName}.");
        }

        return await GetAsync(entity.Id);
    }

    public async Task<SubstitutionDto> UpdateAsync(Guid id, UpdateSubstitutionDto input)
    {
        await EnsureAdminAsync("update substitutions");
        var entity = await substitutionRepository.GetAsync(id);
        if (entity.Status is SubstitutionStatus.Completed or SubstitutionStatus.Cancelled)
        {
            throw new UserFriendlyException("Completed or cancelled substitutions cannot be edited.");
        }

        var date = NormalizeDate(input.Date);
        var start = ParseTime(input.StartTime, "startTime");
        var end = ParseTime(input.EndTime, "endTime");
        if (end <= start)
        {
            throw new UserFriendlyException("End time must be after start time.");
        }
        EnsureSubstitutionWindowNotFinished(date, end, "Cannot update a substitution after the period has ended.");

        var duplicate = await substitutionRepository.FirstOrDefaultAsync(x =>
            x.Id != id &&
            x.Date == date &&
            x.TimetableSlotSubjectId == input.TimetableSlotSubjectId &&
            x.OriginalTeacherId == input.OriginalTeacherId &&
            x.Status != SubstitutionStatus.Cancelled &&
            x.Status != SubstitutionStatus.Rejected &&
            x.Status != SubstitutionStatus.Expired &&
            x.StartTime < end &&
            x.EndTime > start);
        if (duplicate != null)
        {
            throw new UserFriendlyException("This slot time already has an active substitution assignment.");
        }

        if (entity.SubstituteTeacherId.HasValue || !string.IsNullOrWhiteSpace(entity.SubstituteTeacherName))
        {
            await EnsureSubstituteCanBeAssignedAsync(
                date,
                start,
                end,
                input.StandardId,
                input.SubjectId,
                input.OriginalTeacherId,
                input.OriginalTeacherName,
                input.SubjectName,
                entity.SubstituteTeacherId,
                entity.SubstituteTeacherName,
                id,
                input.AcademicMediumId);
            await EnsureAssignedRoomAvailableAsync(
                date,
                start,
                end,
                entity.AssignedRoomId ?? input.OriginalRoomId,
                input.TimetableSlotId,
                id);
        }

        entity.UpdateDetails(
            date,
            input.Period,
            start,
            end,
            input.ClassName ?? string.Empty,
            input.DivisionName ?? string.Empty,
            input.SubjectName ?? string.Empty,
            input.OriginalTeacherName ?? string.Empty,
            input.RoomName ?? string.Empty,
            input.Reason,
            input.Priority,
            input.Source,
            input.AcknowledgementRequired,
            input.AcademicYearId,
            input.StandardId,
            input.DivisionId,
            input.TimetableId,
            input.TimetableSlotId,
            input.TimetableSlotSubjectId,
            input.SubjectId,
            input.OriginalTeacherId,
            input.OriginalRoomId,
            input.SubjectName,
            input.Remarks);

        await substitutionRepository.UpdateAsync(entity, autoSave: true);
        await WriteAuditAsync(id, "Updated", "Substitution details updated.");
        return await GetAsync(id);
    }

    public async Task<SubstitutionDto> AssignAsync(Guid id, AssignSubstituteDto input)
    {
        await EnsureAdminAsync("assign substitutions");
        if (string.IsNullOrWhiteSpace(input.SubstituteTeacherName) && !input.SubstituteTeacherId.HasValue)
        {
            throw new UserFriendlyException("Select a substitute teacher.");
        }

        var entity = await substitutionRepository.GetAsync(id);
        EnsureSubstitutionWindowNotFinished(entity.Date, entity.EndTime, "Cannot assign a substitute after the period has ended.");
        var suggestions = await GetSuggestedTeachersAsync(id);
        var selected = suggestions.FirstOrDefault(x =>
            (input.SubstituteTeacherId.HasValue && x.TeacherId == input.SubstituteTeacherId) ||
            string.Equals(x.TeacherName, input.SubstituteTeacherName, StringComparison.OrdinalIgnoreCase));

        selected = await EnsureSubstituteCanBeAssignedAsync(
            entity.Date,
            entity.StartTime,
            entity.EndTime,
            entity.StandardId,
            entity.SubjectId,
            entity.OriginalTeacherId,
            entity.OriginalTeacherName,
            entity.DepartmentName,
            input.SubstituteTeacherId,
            input.SubstituteTeacherName,
            id,
            await ResolveAcademicMediumIdAsync(entity.SubjectId));
        await EnsureAssignedRoomAvailableAsync(
            entity.Date,
            entity.StartTime,
            entity.EndTime,
            input.AssignedRoomId ?? entity.OriginalRoomId,
            entity.TimetableSlotId,
            id);

        if (selected?.HasConflict == true && selected.ConflictSeverity == "Hard Block")
        {
            throw new UserFriendlyException(selected.ConflictReason ?? "The selected teacher has a hard conflict.");
        }

        entity.Assign(
            input.SubstituteTeacherId ?? selected?.TeacherId,
            string.IsNullOrWhiteSpace(input.SubstituteTeacherName) ? selected?.TeacherName ?? string.Empty : input.SubstituteTeacherName,
            input.AssignedRoomId,
            input.AssignedRoomName,
            selected?.Score,
            selected?.LoadStatus ?? "Low",
            ParseConflict(selected?.ConflictSeverity),
            selected?.ConflictReason,
            input.SendNotification);
        entity.MarkNotificationSent();

        await substitutionRepository.UpdateAsync(entity, autoSave: true);
        await WriteNotificationAsync(entity, "Assignment", entity.SubstituteTeacherName ?? input.SubstituteTeacherName, input.SendNotification ? "Queued" : "Skipped");
        await WriteAuditAsync(entity.Id, "Assigned", $"Assigned to {entity.SubstituteTeacherName}.");
        return await GetAsync(entity.Id);
    }

    public async Task<SubstitutionDto> SendReminderAsync(Guid id)
    {
        await EnsureAdminAsync("send reminders");
        var entity = await substitutionRepository.GetAsync(id);
        EnsureSubstitutionWindowNotFinished(entity.Date, entity.EndTime, "Cannot send a reminder after the period has ended.");
        if (string.IsNullOrWhiteSpace(entity.SubstituteTeacherName))
        {
            throw new UserFriendlyException("Assign a substitute teacher before sending a reminder.");
        }

        entity.MarkNotificationSent();
        await substitutionRepository.UpdateAsync(entity, autoSave: true);
        await WriteNotificationAsync(entity, "Reminder", entity.SubstituteTeacherName, "Queued");
        await WriteAuditAsync(id, "Reminder Sent", $"Reminder sent to {entity.SubstituteTeacherName}.");
        return await GetAsync(id);
    }

    public async Task<SubstitutionDto> CompleteAsync(Guid id)
    {
        var entity = await substitutionRepository.GetAsync(id);
        await EnsureCanActAsSubstituteAsync(entity);
        EnsureSubstitutionWindowStarted(entity.Date, entity.StartTime, "Substitution duty can be completed only after the period has started.");
        if (string.IsNullOrWhiteSpace(entity.SubstituteTeacherName) && !entity.SubstituteTeacherId.HasValue)
        {
            throw new UserFriendlyException("Assign a substitute teacher before completing the duty.");
        }

        if (entity.Status is not (SubstitutionStatus.Assigned or SubstitutionStatus.Acknowledged))
        {
            throw new UserFriendlyException("Only assigned or acknowledged substitution duties can be completed.");
        }

        entity.Complete();
        await substitutionRepository.UpdateAsync(entity, autoSave: true);
        await WriteAuditAsync(id, "Completed", "Substitution marked completed.");
        return await GetAsync(id);
    }

    public async Task<SubstitutionDto> CancelAsync(Guid id, SubstitutionActionDto input)
    {
        await EnsureAdminAsync("cancel substitutions");
        var entity = await substitutionRepository.GetAsync(id);
        entity.Cancel(input.Reason);
        await substitutionRepository.UpdateAsync(entity, autoSave: true);
        await WriteAuditAsync(id, "Cancelled", input.Reason);
        return await GetAsync(id);
    }

    public async Task<SubstitutionDto> RejectAsync(Guid id, SubstitutionActionDto input)
    {
        return await RejectDutyAsync(id, input);
    }

    public async Task<SubstitutionDto> AcknowledgeAsync(Guid id, SubstitutionActionDto? input = null)
    {
        var entity = await substitutionRepository.GetAsync(id);
        await EnsureCanActAsSubstituteAsync(entity);
        EnsureSubstitutionWindowNotFinished(entity.Date, entity.EndTime, "Cannot acknowledge a substitution after the period has ended.");
        var note = NormalizeOptionalActionReason(input?.Reason);
        entity.Acknowledge(note);
        await substitutionRepository.UpdateAsync(entity, autoSave: true);
        await WriteAuditAsync(id, "Acknowledged", note ?? "Duty accepted.");
        return await GetAsync(id);
    }

    public async Task<SubstitutionDto> RequestChangeAsync(Guid id, SubstitutionActionDto input)
    {
        var reason = NormalizeRequiredActionReason(input?.Reason);

        var entity = await substitutionRepository.GetAsync(id);
        await EnsureCanActAsSubstituteAsync(entity);
        EnsureSubstitutionWindowNotFinished(entity.Date, entity.EndTime, "Cannot request changes after the period has ended.");
        entity.RequestChange(reason);
        await substitutionRepository.UpdateAsync(entity, autoSave: true);
        await WriteAuditAsync(id, "Change Requested", reason);
        return await GetAsync(id);
    }

    public async Task<SubstitutionDto> RejectDutyAsync(Guid id, SubstitutionActionDto input)
    {
        var reason = NormalizeRequiredActionReason(input?.Reason);

        var entity = await substitutionRepository.GetAsync(id);
        await EnsureCanActAsSubstituteAsync(entity);
        EnsureSubstitutionWindowNotFinished(entity.Date, entity.EndTime, "Cannot reject a substitution after the period has ended.");
        entity.Reject(reason);
        await substitutionRepository.UpdateAsync(entity, autoSave: true);
        await WriteAuditAsync(id, "Rejected", reason);
        return await GetAsync(id);
    }

    public async Task<List<SuggestedTeacherDto>> GetSuggestedTeachersAsync(Guid id)
    {
        var entity = await substitutionRepository.GetAsync(id);
        return await BuildSuggestedTeachersAsync(
            entity.Date,
            entity.StartTime,
            entity.EndTime,
            entity.StandardId,
            entity.SubjectId,
            entity.OriginalTeacherId,
            entity.OriginalTeacherName,
            entity.DepartmentName,
            id,
            await ResolveAcademicMediumIdAsync(entity.SubjectId));
    }

    public async Task<SubstitutionCreateFormMetadataDto> GetCreateFormMetadataAsync(SubstitutionCreateFormMetadataInput input)
    {
        var subjectQuery = await subjectRepository.GetQueryableAsync();
        if (input.AcademicMediumId.HasValue && input.AcademicMediumId.Value != Guid.Empty)
        {
            subjectQuery = subjectQuery.Where(x => x.EducationMediumId == input.AcademicMediumId.Value);
        }

        var subjects = await AsyncExecuter.ToListAsync(subjectQuery);
        var scopedSubjectIds = subjects.Select(x => x.Id).ToHashSet();
        var assignedSubjectQuery = await assignedSubjectRepository.WithDetailsAsync(x => x.Teachers);
        if (input.AcademicMediumId.HasValue && input.AcademicMediumId.Value != Guid.Empty)
        {
            assignedSubjectQuery = assignedSubjectQuery.Where(x => scopedSubjectIds.Contains(x.SubjectId));
        }

        var assignedSubjects = await AsyncExecuter.ToListAsync(assignedSubjectQuery);
        var assignedStandardIds = assignedSubjects.Select(x => x.StandardId).Distinct().ToList();
        var assignedSubjectIds = assignedSubjects.Select(x => x.SubjectId).Distinct().ToList();
        var assignedTeacherUserIds = assignedSubjects
            .SelectMany(x => x.Teachers)
            .Select(x => x.TeacherId)
            .Distinct()
            .ToList();
        var standards = assignedStandardIds.Count > 0
            ? await standardRepository.GetListAsync(x => assignedStandardIds.Contains(x.Id))
            : [];
        var divisions = await divisionRepository.GetListAsync();
        subjects = subjects
            .Where(x => assignedSubjectIds.Contains(x.Id))
            .ToList();
        var rooms = await roomRepository.GetListAsync();
        var employees = assignedTeacherUserIds.Count > 0
            ? await employeeRepository.GetListAsync(x =>
                (assignedTeacherUserIds.Contains(x.Id) ||
                 (x.UserId.HasValue && assignedTeacherUserIds.Contains(x.UserId.Value))) &&
                (!x.EmployeeStatusType.HasValue || x.EmployeeStatusType == EmployeeStatusType.Active))
            : [];
        var employeeById = employees.ToDictionary(x => x.Id);
        var employeeByUserId = employees
            .Where(x => x.UserId.HasValue)
            .GroupBy(x => x.UserId!.Value)
            .ToDictionary(x => x.Key, x => x.First());
        var subjectById = subjects.ToDictionary(x => x.Id);

        return new SubstitutionCreateFormMetadataDto
        {
            Standards = standards
                .OrderBy(x => x.Name)
                .Select(x => new SubstitutionLookupOptionDto { Id = x.Id, Name = x.Name })
                .ToList(),
            Divisions = divisions
                .Where(x => assignedStandardIds.Count == 0 || assignedStandardIds.Contains(x.StandardId))
                .OrderBy(x => x.Name)
                .Select(x => new SubstitutionDivisionOptionDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    StandardId = x.StandardId
                })
                .ToList(),
            Subjects = assignedSubjects
                .Where(x => subjectById.ContainsKey(x.SubjectId))
                .OrderBy(x => standards.FirstOrDefault(standard => standard.Id == x.StandardId)?.Name)
                .ThenBy(x => subjectById[x.SubjectId].Name)
                .Select(x =>
                {
                    var primaryTeacherId = x.Teachers.FirstOrDefault(t => t.TeacherType == SubjectTeacherType.Primary)?.TeacherId;
                    var primaryEmployee = primaryTeacherId.HasValue
                        ? employeeByUserId.GetValueOrDefault(primaryTeacherId.Value) ??
                          employeeById.GetValueOrDefault(primaryTeacherId.Value)
                        : null;
                    return new SubstitutionSubjectOptionDto
                    {
                        Id = x.SubjectId,
                        Name = subjectById[x.SubjectId].Name,
                        StandardId = x.StandardId,
                        AssignedSubjectId = x.Id,
                        PrimaryTeacherId = primaryEmployee?.Id,
                        PrimaryTeacherName = primaryEmployee?.EmployeeName
                    };
                })
                .ToList(),
            Teachers = employees
                .OrderBy(x => x.EmployeeName)
                .Select(x => new SubstitutionTeacherOptionDto
                {
                    Id = x.Id,
                    Name = x.EmployeeName,
                    DepartmentName = x.Department ?? string.Empty
                })
                .ToList(),
            Rooms = rooms
                .OrderBy(x => x.RoomName)
                .Select(x => new SubstitutionLookupOptionDto { Id = x.Id, Name = x.RoomName })
                .ToList()
        };
    }

    public async Task<List<SubstitutionLectureLookupDto>> GetLecturesAsync(SubstitutionLectureLookupInput input)
    {
        if (input.StandardId == Guid.Empty || input.DivisionId == Guid.Empty)
        {
            return [];
        }

        var tz = GetInstitutionTimeZone();
        var lectureDate = input.Date == default ? DateTimeOffset.Now : input.Date;
        var localLectureDate = TimeZoneInfo.ConvertTime(lectureDate, tz);
        var timetableDay = ToTimetableDay(localLectureDate.DayOfWeek);
        if (!timetableDay.HasValue)
        {
            return [];
        }

        var timetable = await timetableRepository.FindByStandardAndDivisionAsync(input.StandardId, input.DivisionId);
        if (timetable == null)
        {
            return [];
        }

        var standard = await standardRepository.FindAsync(input.StandardId);
        var division = await divisionRepository.FindAsync(input.DivisionId);
        var subjects = await subjectRepository.GetListAsync();
        var rooms = await roomRepository.GetListAsync();
        var employees = await employeeRepository.GetListAsync();

        return timetable.Slots
            .Where(slot => slot.Day == timetableDay.Value && slot.SlotType == SlotType.Period)
            .SelectMany(slot => slot.Subjects.Select(slotSubject =>
            {
                var subject = subjects.FirstOrDefault(x => x.Id == slotSubject.SubjectId);
                var teacher = ResolveEmployeeByTeacherReference(employees, slotSubject.TeacherId, null);
                var room = slotSubject.RoomId.HasValue
                    ? rooms.FirstOrDefault(x => x.Id == slotSubject.RoomId.Value)
                    : null;

                return new SubstitutionLectureLookupDto
                {
                    TimetableId = timetable.Id,
                    TimetableSlotId = slot.Id,
                    SlotSubjectId = slotSubject.Id,
                    Period = $"Period {slot.Sequence}",
                    StartTime = slot.StartTime.ToString("HH:mm"),
                    EndTime = slot.EndTime.ToString("HH:mm"),
                    StandardId = timetable.StandardId,
                    ClassName = standard?.Name ?? string.Empty,
                    DivisionId = timetable.DivisionId,
                    DivisionName = division?.Name ?? string.Empty,
                    SubjectId = slotSubject.SubjectId,
                    SubjectName = subject?.Name ?? string.Empty,
                    OriginalTeacherId = teacher?.Id ?? slotSubject.TeacherId,
                    OriginalTeacherName = teacher?.EmployeeName ?? string.Empty,
                    OriginalRoomId = slotSubject.RoomId,
                    RoomName = room?.RoomName ?? string.Empty
                };
            }))
            .OrderBy(x => x.StartTime)
            .ThenBy(x => x.SubjectName)
            .ToList();
    }

    public async Task<List<SuggestedTeacherDto>> GetSuggestedTeachersAsync(SuggestedTeacherInput input)
    {
        var date = NormalizeDate(input.Date == default ? DateTimeOffset.Now : input.Date);
        var start = string.IsNullOrWhiteSpace(input.StartTime)
            ? TimeOnly.MinValue
            : ParseTime(input.StartTime, "startTime");
        var end = string.IsNullOrWhiteSpace(input.EndTime)
            ? new TimeOnly(23, 59)
            : ParseTime(input.EndTime, "endTime");
        if (end <= start)
        {
            throw new UserFriendlyException("End time must be after start time.");
        }

        return await BuildSuggestedTeachersAsync(
            date,
            start,
            end,
            input.StandardId,
            input.SubjectId,
            input.OriginalTeacherId,
            input.OriginalTeacherName,
            input.DepartmentName ?? input.SubjectName,
            null,
            input.AcademicMediumId);
    }

    private async Task<SuggestedTeacherDto> EnsureSubstituteCanBeAssignedAsync(
        DateTime date,
        TimeOnly startTime,
        TimeOnly endTime,
        Guid? standardId,
        Guid? subjectId,
        Guid? originalTeacherId,
        string? originalTeacherName,
        string? departmentName,
        Guid? substituteTeacherId,
        string? substituteTeacherName,
        Guid? currentSubstitutionId,
        Guid? academicMediumId)
    {
        var suggestions = await BuildSuggestedTeachersAsync(
            date,
            startTime,
            endTime,
            standardId,
            subjectId,
            originalTeacherId,
            originalTeacherName,
            departmentName,
            currentSubstitutionId,
            academicMediumId);

        var selected = suggestions.FirstOrDefault(x =>
            (substituteTeacherId.HasValue && x.TeacherId == substituteTeacherId) ||
            (!string.IsNullOrWhiteSpace(substituteTeacherName) &&
             string.Equals(x.TeacherName, substituteTeacherName, StringComparison.OrdinalIgnoreCase)));

        if (selected == null)
        {
            throw new UserFriendlyException("Selected substitute teacher is inactive, unavailable, or not found.");
        }

        if (selected.HasConflict && selected.ConflictSeverity == "Hard Block")
        {
            throw new UserFriendlyException(selected.ConflictReason ?? "The selected teacher has a hard conflict.");
        }

        return selected;
    }

    private async Task EnsureAssignedRoomAvailableAsync(
        DateTime date,
        TimeOnly startTime,
        TimeOnly endTime,
        Guid? assignedRoomId,
        Guid? currentTimetableSlotId,
        Guid? currentSubstitutionId)
    {
        if (!assignedRoomId.HasValue)
        {
            return;
        }

        var activeStatuses = new[]
        {
            SubstitutionStatus.Pending,
            SubstitutionStatus.Assigned,
            SubstitutionStatus.WaitingAck,
            SubstitutionStatus.Acknowledged,
            SubstitutionStatus.Conflict
        };

        var duplicateSubstitution = await substitutionRepository.FirstOrDefaultAsync(x =>
            (!currentSubstitutionId.HasValue || x.Id != currentSubstitutionId.Value) &&
            x.Date == date &&
            activeStatuses.Contains(x.Status) &&
            (x.AssignedRoomId == assignedRoomId.Value || x.OriginalRoomId == assignedRoomId.Value) &&
            x.StartTime < endTime &&
            x.EndTime > startTime);
        if (duplicateSubstitution != null)
        {
            throw new UserFriendlyException("Selected room already has an active substitution during this time.");
        }

        var timetableDay = ToTimetableDay(date.DayOfWeek);
        if (!timetableDay.HasValue)
        {
            return;
        }

        var publishedTimetables = await timetableRepository.GetListAsync(x => x.Status == TimetableStatus.Published);
        var roomConflict = publishedTimetables
            .SelectMany(x => x.Slots)
            .Where(slot =>
                (!currentTimetableSlotId.HasValue || slot.Id != currentTimetableSlotId.Value) &&
                slot.Day == timetableDay.Value &&
                slot.SlotType == SlotType.Period &&
                HasOverlap(slot.StartTime, slot.EndTime, startTime, endTime))
            .SelectMany(slot => slot.Subjects)
            .Any(subject => subject.RoomId == assignedRoomId.Value);

        if (roomConflict)
        {
            throw new UserFriendlyException("Selected room is already booked in a published timetable during this time.");
        }
    }

    private async Task<List<SuggestedTeacherDto>> BuildSuggestedTeachersAsync(
        DateTime date,
        TimeOnly startTime,
        TimeOnly endTime,
        Guid? standardId,
        Guid? subjectId,
        Guid? originalTeacherId,
        string? originalTeacherName,
        string? departmentName,
        Guid? currentSubstitutionId,
        Guid? academicMediumId)
    {
        var effectiveAcademicMediumId = academicMediumId ?? await ResolveAcademicMediumIdAsync(subjectId);
        var contextTeacherUserIds = await GetTeacherUserIdsForAcademicMediumAsync(effectiveAcademicMediumId);
        var employees = await employeeRepository.GetListAsync(x =>
            !x.EmployeeStatusType.HasValue || x.EmployeeStatusType == EmployeeStatusType.Active);
        if (effectiveAcademicMediumId.HasValue && effectiveAcademicMediumId.Value != Guid.Empty)
        {
            employees = employees
                .Where(x =>
                    contextTeacherUserIds.Contains(x.Id) ||
                    (x.UserId.HasValue && contextTeacherUserIds.Contains(x.UserId.Value)))
                .ToList();
        }

        var sameDay = await substitutionRepository.GetListAsync(x => x.Date == date);
        var unavailable = await availabilityRepository.GetListAsync(x =>
            x.AvailabilityDate == date &&
            !x.IsCancelled &&
            x.StartTime < endTime &&
            x.EndTime > startTime);
        var allocationTeacherUserIds = new HashSet<Guid>();
        if (subjectId.HasValue)
        {
            var assignedSubjectQuery = await assignedSubjectRepository.WithDetailsAsync(x => x.Teachers);
            allocationTeacherUserIds = (await AsyncExecuter.ToListAsync(assignedSubjectQuery.Where(x =>
                    x.SubjectId == subjectId.Value &&
                    (!standardId.HasValue || x.StandardId == standardId.Value))))
                .SelectMany(x => x.Teachers)
                .Select(x => x.TeacherId)
                .Distinct()
                .ToHashSet();
        }
        var timetableDay = ToTimetableDay(date.DayOfWeek);
        var timetables = timetableDay.HasValue ? await timetableRepository.GetListAsync() : [];

        return employees
            .Where(x =>
                (!originalTeacherId.HasValue || x.Id != originalTeacherId.Value) &&
                !string.Equals(x.EmployeeName, originalTeacherName, StringComparison.OrdinalIgnoreCase))
            .Select(employee =>
            {
                var todayCount = sameDay.Count(x => x.SubstituteTeacherId == employee.Id);
                var weekCount = sameDay.Count(x => x.SubstituteTeacherId == employee.Id);
                var blockingIntervals = unavailable
                    .Where(x =>
                        (x.TeacherId.HasValue && x.TeacherId == employee.Id) ||
                        string.Equals(x.TeacherName, employee.EmployeeName, StringComparison.OrdinalIgnoreCase))
                    .Select(x => (Start: x.StartTime, End: x.EndTime))
                    .Concat(sameDay
                        .Where(x =>
                            (!currentSubstitutionId.HasValue || x.Id != currentSubstitutionId.Value) &&
                            x.SubstituteTeacherId == employee.Id &&
                            x.Status != SubstitutionStatus.Cancelled &&
                            x.Status != SubstitutionStatus.Rejected &&
                            x.Status != SubstitutionStatus.Expired &&
                            HasOverlap(x.StartTime, x.EndTime, startTime, endTime))
                        .Select(x => (Start: x.StartTime, End: x.EndTime)))
                    .Concat(timetables
                        .SelectMany(timetable => timetable.Slots)
                        .Where(slot =>
                            timetableDay.HasValue &&
                            slot.Day == timetableDay.Value &&
                            slot.SlotType == SlotType.Period &&
                            HasOverlap(slot.StartTime, slot.EndTime, startTime, endTime) &&
                            slot.Subjects.Any(subject => subject.TeacherId == employee.Id))
                        .Select(slot => (Start: slot.StartTime, End: slot.EndTime)))
                    .ToList();
                var freeSegments = GetFreeSegments(startTime, endTime, blockingIntervals);
                var bestSegment = freeSegments
                    .OrderByDescending(segment => GetMinutes(segment.Start, segment.End))
                    .FirstOrDefault();
                var hasFullConflict = bestSegment == default;
                var coverageMinutes = hasFullConflict ? 0 : GetMinutes(bestSegment.Start, bestSegment.End);
                var isFullCoverage = !hasFullConflict && bestSegment.Start <= startTime && bestSegment.End >= endTime;
                var isPartialCoverage = !hasFullConflict && !isFullCoverage;
                var assignedToSubject =
                    allocationTeacherUserIds.Contains(employee.Id) ||
                    (employee.UserId.HasValue && allocationTeacherUserIds.Contains(employee.UserId.Value));
                var sameDepartment = !string.IsNullOrWhiteSpace(employee.Department) &&
                    string.Equals(employee.Department, departmentName, StringComparison.OrdinalIgnoreCase);
                var canTeachSubject = assignedToSubject || sameDepartment;
                var score = 55 + (assignedToSubject ? 30 : sameDepartment ? 20 : 0) - (todayCount * 8) + (isFullCoverage ? 10 : 0);
                if (hasFullConflict)
                {
                    score = 0;
                }
                else if (isPartialCoverage)
                {
                    score = Math.Min(score, 70);
                }

                return new SuggestedTeacherDto
                {
                    TeacherId = employee.Id,
                    TeacherName = employee.EmployeeName,
                    DepartmentName = employee.Department ?? string.Empty,
                    CanTeachSubject = canTeachSubject,
                    HasConflict = hasFullConflict,
                    ConflictSeverity = hasFullConflict ? "Hard Block" : isPartialCoverage || !canTeachSubject ? "Warning" : "Clear",
                    ConflictReason = hasFullConflict
                        ? "Teacher is occupied or unavailable for the complete selected slot."
                        : isPartialCoverage
                            ? $"Teacher is free only from {bestSegment.Start:HH:mm} to {bestSegment.End:HH:mm}."
                            : canTeachSubject
                                ? null
                                : "Different department or subject expertise not confirmed.",
                    TodaySubstitutionCount = todayCount,
                    WeekSubstitutionCount = weekCount,
                    MonthSubstitutionCount = weekCount,
                    TodayTeachingLoad = 0,
                    FreePeriods = hasFullConflict ? 0 : 1,
                    LastDutyDate = null,
                    LoadStatus = todayCount >= 3 ? "High" : todayCount >= 1 ? "Medium" : "Low",
                    Score = Math.Clamp(score, 0, 100),
                    CoverageStartTime = hasFullConflict ? string.Empty : bestSegment.Start.ToString("HH:mm"),
                    CoverageEndTime = hasFullConflict ? string.Empty : bestSegment.End.ToString("HH:mm"),
                    CoverageMinutes = coverageMinutes,
                    IsFullSlotCoverage = isFullCoverage,
                    IsPartialCoverage = isPartialCoverage,
                    RecommendationReason = isPartialCoverage
                        ? $"Partial coverage available from {bestSegment.Start:HH:mm} to {bestSegment.End:HH:mm}."
                        : assignedToSubject
                            ? "Free during selected period and assigned to this subject in Subject Allocation."
                            : sameDepartment
                                ? "Free during selected period and department matches the subject."
                            : "Free during selected period; subject expertise should be reviewed."
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.TeacherName)
            .Take(20)
            .ToList();
    }

    public async Task<List<SubstitutionAuditLogDto>> GetAuditLogsAsync(Guid id)
    {
        var rows = await auditRepository.GetListAsync(x => x.SubstitutionRequestId == id);
        return rows.OrderByDescending(x => x.Timestamp).Select(MapAudit).ToList();
    }

    public async Task<PagedResultDto<SubstitutionDto>> GetMyDutiesAsync(GetSubstitutionsInput input)
    {
        var employee = await ResolveCurrentEmployeeAsync();
        if (employee == null)
        {
            return new PagedResultDto<SubstitutionDto>(0, []);
        }

        input.SubstituteTeacherId = employee.Id;
        return await GetListAsync(input);
    }

    public async Task<SubstitutionDailySheetDto> GetDailySheetAsync(SubstitutionDailySheetInput input)
    {
        var date = NormalizeDate(input.Date ?? input.FromDate ?? DateTimeOffset.Now);
        input.Date = new DateTimeOffset(date, TimeSpan.Zero);
        input.FromDate = null;
        input.ToDate = null;
        input.MaxResultCount = input.MaxResultCount <= 0 ? 500 : input.MaxResultCount;
        var rows = await GetListAsync(input);
        return new SubstitutionDailySheetDto
        {
            Date = date,
            GeneratedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"),
            Items = rows.Items.ToList()
        };
    }

    public async Task<byte[]> GetDailySheetExcelAsync(SubstitutionDailySheetInput input)
    {
        var sheet = await GetDailySheetAsync(input);
        var builder = new StringBuilder();
        builder.AppendLine("Date,Period,Time,Class,Division,Subject,Original Teacher,Substitute Teacher,Original Room,Assigned Room,Status,Acknowledgement,Remarks");
        foreach (var row in sheet.Items)
        {
            builder.AppendLine(string.Join(",", [
                EscapeCsv(row.Date.ToString("yyyy-MM-dd")),
                EscapeCsv(row.Period),
                EscapeCsv(row.Time),
                EscapeCsv(row.ClassName),
                EscapeCsv(row.DivisionName),
                EscapeCsv(row.SubjectName),
                EscapeCsv(row.OriginalTeacherName),
                EscapeCsv(row.SubstituteTeacherName ?? "Unassigned"),
                EscapeCsv(row.RoomName),
                EscapeCsv(row.AssignedRoomName ?? row.RoomName),
                EscapeCsv(row.Status),
                EscapeCsv(row.AcknowledgementStatus),
                EscapeCsv(input.StudentVisible ? string.Empty : row.Remarks ?? string.Empty)
            ]));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public async Task<byte[]> GetDailySheetPdfAsync(SubstitutionDailySheetInput input)
    {
        var sheet = await GetDailySheetAsync(input);
        var text = new StringBuilder();
        text.AppendLine("%PDF-1.4");
        text.AppendLine("% Substitution daily sheet export");
        text.AppendLine($"Daily Substitution Sheet - {sheet.Date:yyyy-MM-dd}");
        foreach (var row in sheet.Items)
        {
            text.AppendLine($"{row.Period} {row.Time} | {row.ClassName}-{row.DivisionName} | {row.SubjectName} | {row.OriginalTeacherName} -> {row.SubstituteTeacherName ?? "Unassigned"} | {row.AssignedRoomName ?? row.RoomName}");
        }

        return Encoding.UTF8.GetBytes(text.ToString());
    }

    public async Task<SubstitutionAnalyticsDto> GetAnalyticsAsync(SubstitutionAnalyticsInput input)
    {
        var rows = await substitutionRepository.GetListAsync();
        if (!IsAdminUser() && CurrentUser.Id.HasValue)
        {
            var visibleTeacherIds = await GetVisibleTeacherReferenceIdsAsync(CurrentUser.Id.Value);
            rows = rows.Where(x =>
                (x.OriginalTeacherId.HasValue && visibleTeacherIds.Contains(x.OriginalTeacherId.Value)) ||
                (x.SubstituteTeacherId.HasValue && visibleTeacherIds.Contains(x.SubstituteTeacherId.Value))).ToList();
        }
        var filtered = rows.AsEnumerable();
        if (input.AcademicYearId.HasValue) filtered = filtered.Where(x => x.AcademicYearId == input.AcademicYearId);
        if (input.FromDate.HasValue) filtered = filtered.Where(x => x.Date >= NormalizeDate(input.FromDate.Value));
        if (input.ToDate.HasValue) filtered = filtered.Where(x => x.Date <= NormalizeDate(input.ToDate.Value));
        if (input.StandardId.HasValue) filtered = filtered.Where(x => x.StandardId == input.StandardId);
        if (input.DivisionId.HasValue) filtered = filtered.Where(x => x.DivisionId == input.DivisionId);
        if (input.TeacherId.HasValue)
        {
            filtered = filtered.Where(x => x.OriginalTeacherId == input.TeacherId || x.SubstituteTeacherId == input.TeacherId);
        }
        if (input.SubjectId.HasValue) filtered = filtered.Where(x => x.SubjectId == input.SubjectId);

        var list = filtered.ToList();
        var total = list.Count;
        var covered = list.Count(x => !string.IsNullOrWhiteSpace(x.SubstituteTeacherName) && x.Status != SubstitutionStatus.Cancelled);
        var acknowledgedRows = list.Where(x => x.AcknowledgedAt.HasValue).ToList();
        return new SubstitutionAnalyticsDto
        {
            Total = total,
            Covered = covered,
            Uncovered = list.Count(x => x.Status is SubstitutionStatus.Pending or SubstitutionStatus.Conflict),
            Completed = list.Count(x => x.Status == SubstitutionStatus.Completed),
            Rejected = list.Count(x => x.AcknowledgementStatus == SubstitutionAcknowledgementStatus.Rejected),
            Conflicts = list.Count(x => x.ConflictStatus == SubstitutionConflictStatus.HardBlock || x.Status == SubstitutionStatus.Conflict),
            CoverageRate = total == 0 ? 0 : Math.Round((decimal) covered * 100 / total, 2),
            AverageAcknowledgementMinutes = acknowledgedRows.Count == 0
                ? 0
                : Math.Round(acknowledgedRows.Average(x => (x.AcknowledgedAt!.Value - x.CreationTime).TotalMinutes).ToDecimalSafe(), 2),
            TopSubstituteTeacher = list
                .Where(x => !string.IsNullOrWhiteSpace(x.SubstituteTeacherName))
                .GroupBy(x => x.SubstituteTeacherName!)
                .OrderByDescending(x => x.Count())
                .Select(x => x.Key)
                .FirstOrDefault() ?? string.Empty,
            FrequentSubstitutes = BuildBreakdown(list.Where(x => !string.IsNullOrWhiteSpace(x.SubstituteTeacherName)), x => x.SubstituteTeacherName!),
            RepeatedOriginalTeacherAbsences = BuildBreakdown(list, x => x.OriginalTeacherName),
            SubjectCoverageLoss = BuildBreakdown(list.Where(x => x.Status is SubstitutionStatus.Pending or SubstitutionStatus.Conflict), x => x.SubjectName),
            WorkloadPatterns = BuildBreakdown(list.Where(x => !string.IsNullOrWhiteSpace(x.WorkloadStatus)), x => x.WorkloadStatus)
        };
    }

    public async Task<SubstitutionSettingsDto> GetSettingsAsync(Guid? academicYearId = null)
    {
        var entity = await GetOrCreateSettingsAsync(academicYearId);
        return MapSettings(entity);
    }

    public async Task<SubstitutionSettingsDto> UpdateSettingsAsync(SubstitutionSettingsDto input)
    {
        var entity = await GetOrCreateSettingsAsync(input.AcademicYearId);
        entity.Update(
            input.MaxSubstitutionsPerTeacherPerDay,
            input.MaxSubstitutionsPerTeacherPerWeek,
            input.AcknowledgementRequiredByDefault,
            input.AutoReminderEnabled,
            input.ReminderBeforeMinutes,
            input.NotifyLeadershipForUrgent,
            input.AllowTeacherReject,
            input.StudentVisibleDailySheet);
        await settingRepository.UpdateAsync(entity, autoSave: true);
        return MapSettings(entity);
    }

    private async Task<Employee?> ResolveCurrentEmployeeAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            return null;
        }

        return await employeeRepository.FirstOrDefaultAsync(x => x.UserId == CurrentUser.Id.Value);
    }

    private async Task WriteAuditAsync(Guid substitutionId, string action, string? details)
    {
        var actor = CurrentUser.Name ?? CurrentUser.UserName ?? "System";
        await auditRepository.InsertAsync(
            new SubstitutionAuditLog(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                substitutionId,
                actor,
                action,
                details,
                CurrentUser.Id),
            autoSave: true);
    }

    private static string? NormalizeOptionalActionReason(string? reason)
    {
        var trimmed = reason?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string NormalizeRequiredActionReason(string? reason)
    {
        var trimmed = NormalizeOptionalActionReason(reason);
        if (trimmed is null || trimmed.Length < 5)
        {
            throw new UserFriendlyException("Reason must be at least 5 characters.");
        }

        return trimmed;
    }

    private async Task WriteNotificationAsync(SubstitutionRequest entity, string eventName, string recipientName, string status)
    {
        await notificationRepository.InsertAsync(
            new SubstitutionNotificationLog(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                entity.Id,
                eventName,
                recipientName,
                "InApp",
                status,
                $"{eventName} notification for {entity.Period} {entity.ClassName}-{entity.DivisionName}."),
            autoSave: true);
    }

    private async Task<SubstitutionSetting> GetOrCreateSettingsAsync(Guid? academicYearId)
    {
        var entity = await settingRepository.FirstOrDefaultAsync(x => x.AcademicYearId == academicYearId);
        if (entity != null)
        {
            return entity;
        }

        entity = new SubstitutionSetting(GuidGenerator.Create(), CurrentTenant.Id, academicYearId);
        await settingRepository.InsertAsync(entity, autoSave: true);
        return entity;
    }

    private async Task<Dictionary<Guid, List<SubstitutionAuditLogDto>>> GetAuditLookupAsync(List<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await auditRepository.GetListAsync(x => ids.Contains(x.SubstitutionRequestId));
        return rows
            .OrderByDescending(x => x.Timestamp)
            .GroupBy(x => x.SubstitutionRequestId)
            .ToDictionary(x => x.Key, x => x.Select(MapAudit).ToList());
    }

    private async Task<List<SubstitutionRequest>> FilterByAcademicMediumAsync(
        List<SubstitutionRequest> rows,
        Guid? academicMediumId)
    {
        if (!academicMediumId.HasValue || academicMediumId.Value == Guid.Empty || rows.Count == 0)
        {
            return rows;
        }

        var subjectIds = await GetSubjectIdsForAcademicMediumAsync(academicMediumId);
        return rows
            .Where(x => x.SubjectId.HasValue && subjectIds.Contains(x.SubjectId.Value))
            .ToList();
    }

    private async Task<HashSet<Guid>> GetSubjectIdsForAcademicMediumAsync(Guid? academicMediumId)
    {
        if (!academicMediumId.HasValue || academicMediumId.Value == Guid.Empty)
        {
            return [];
        }

        var subjectQuery = await subjectRepository.GetQueryableAsync();
        return (await AsyncExecuter.ToListAsync(subjectQuery
                .Where(x => x.EducationMediumId == academicMediumId.Value)
                .Select(x => x.Id)))
            .ToHashSet();
    }

    private async Task<HashSet<Guid>> GetTeacherUserIdsForAcademicMediumAsync(Guid? academicMediumId)
    {
        var subjectIds = await GetSubjectIdsForAcademicMediumAsync(academicMediumId);
        if (subjectIds.Count == 0)
        {
            return [];
        }

        var assignedSubjectQuery = await assignedSubjectRepository.WithDetailsAsync(x => x.Teachers);
        return (await AsyncExecuter.ToListAsync(assignedSubjectQuery
                .Where(x => subjectIds.Contains(x.SubjectId))))
            .SelectMany(x => x.Teachers)
            .Select(x => x.TeacherId)
            .Distinct()
            .ToHashSet();
    }

    private async Task<Guid?> ResolveAcademicMediumIdAsync(Guid? subjectId)
    {
        if (!subjectId.HasValue || subjectId.Value == Guid.Empty)
        {
            return null;
        }

        var subject = await subjectRepository.FindAsync(subjectId.Value);
        return subject?.EducationMediumId;
    }

    private static IEnumerable<SubstitutionRequest> ApplyFilters(
        IEnumerable<SubstitutionRequest> rows,
        GetSubstitutionsInput input)
    {
        if (input.AcademicYearId.HasValue) rows = rows.Where(x => x.AcademicYearId == input.AcademicYearId);
        if (input.Date.HasValue) rows = rows.Where(x => x.Date == NormalizeDate(input.Date.Value));
        if (input.FromDate.HasValue) rows = rows.Where(x => x.Date >= NormalizeDate(input.FromDate.Value));
        if (input.ToDate.HasValue) rows = rows.Where(x => x.Date <= NormalizeDate(input.ToDate.Value));
        if (input.StandardId.HasValue) rows = rows.Where(x => x.StandardId == input.StandardId);
        if (input.DivisionId.HasValue) rows = rows.Where(x => x.DivisionId == input.DivisionId);
        if (input.SubjectId.HasValue) rows = rows.Where(x => x.SubjectId == input.SubjectId);
        if (input.OriginalTeacherId.HasValue) rows = rows.Where(x => x.OriginalTeacherId == input.OriginalTeacherId);
        if (input.SubstituteTeacherId.HasValue) rows = rows.Where(x => x.SubstituteTeacherId == input.SubstituteTeacherId);
        if (input.RoomId.HasValue) rows = rows.Where(x => x.OriginalRoomId == input.RoomId || x.AssignedRoomId == input.RoomId);
        if (input.Status.HasValue) rows = rows.Where(x => x.Status == input.Status);
        if (!string.IsNullOrWhiteSpace(input.Reason)) rows = rows.Where(x => x.Reason == input.Reason);
        if (input.Priority.HasValue) rows = rows.Where(x => x.Priority == input.Priority);
        if (input.AcknowledgementStatus.HasValue) rows = rows.Where(x => x.AcknowledgementStatus == input.AcknowledgementStatus);
        if (input.Source.HasValue) rows = rows.Where(x => x.Source == input.Source);
        if (input.ConflictStatus.HasValue) rows = rows.Where(x => x.ConflictStatus == input.ConflictStatus);
        if (!string.IsNullOrWhiteSpace(input.TeacherName))
        {
            rows = rows.Where(x =>
                string.Equals(x.OriginalTeacherName, input.TeacherName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.SubstituteTeacherName, input.TeacherName, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(input.ClassName)) rows = rows.Where(x => x.ClassName == input.ClassName);
        if (!string.IsNullOrWhiteSpace(input.DivisionName)) rows = rows.Where(x => x.DivisionName == input.DivisionName);
        if (!string.IsNullOrWhiteSpace(input.SubjectName)) rows = rows.Where(x => x.SubjectName == input.SubjectName);
        return rows;
    }

    private async Task<SubstitutionDto> MapAsync(SubstitutionRequest row, List<SubstitutionAuditLogDto> auditLogs)
    {
        var dto = Map(row, auditLogs);
        if (!string.IsNullOrWhiteSpace(dto.OriginalTeacherName))
        {
            return dto;
        }

        var employees = await employeeRepository.GetListAsync();
        var employee = ResolveEmployeeByTeacherReference(employees, row.OriginalTeacherId, row.OriginalTeacherName);
        if (employee != null)
        {
            dto.OriginalTeacherId = employee.Id;
            dto.OriginalTeacherName = employee.EmployeeName;
        }

        return dto;
    }

    private static SubstitutionDto Map(SubstitutionRequest row, List<SubstitutionAuditLogDto> auditLogs) => new()
    {
        Id = row.Id,
        Date = row.Date,
        Day = row.Day,
        Period = row.Period,
        StartTime = row.StartTime.ToString("HH:mm"),
        EndTime = row.EndTime.ToString("HH:mm"),
        Time = $"{row.StartTime:HH:mm} - {row.EndTime:HH:mm}",
        StandardId = row.StandardId,
        ClassName = row.ClassName,
        DivisionId = row.DivisionId,
        DivisionName = row.DivisionName,
        SubjectId = row.SubjectId,
        SubjectName = row.SubjectName,
        DepartmentName = row.DepartmentName,
        OriginalTeacherId = row.OriginalTeacherId,
        OriginalTeacherName = row.OriginalTeacherName,
        SubstituteTeacherId = row.SubstituteTeacherId,
        SubstituteTeacherName = row.SubstituteTeacherName,
        OriginalRoomId = row.OriginalRoomId,
        RoomName = row.RoomName,
        AssignedRoomId = row.AssignedRoomId,
        AssignedRoomName = row.AssignedRoomName,
        Reason = row.Reason,
        Status = FormatStatus(row.Status),
        Priority = row.Priority.ToString(),
        AcknowledgementStatus = FormatAck(row.AcknowledgementStatus),
        NotificationStatus = row.NotificationStatus,
        Source = FormatSource(row.Source),
        ConflictStatus = FormatConflict(row.ConflictStatus),
        ConflictReason = row.ConflictReason,
        AiScore = row.SuggestionScore,
        IsAiSuggested = row.Source == SubstitutionSource.Suggested,
        WorkloadStatus = row.WorkloadStatus,
        AcknowledgementRequired = row.AcknowledgementRequired,
        CreatedAt = row.CreationTime,
        AcknowledgedAt = row.AcknowledgedAt?.ToString("HH:mm"),
        CompletedAt = row.CompletedAt?.ToString("HH:mm"),
        Remarks = row.Remarks,
        AuditLog = auditLogs
    };

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

    private static SubstitutionAuditLogDto MapAudit(SubstitutionAuditLog row) => new()
    {
        Id = row.Id,
        At = row.Timestamp.ToString("HH:mm"),
        Actor = row.ActorName,
        Action = string.IsNullOrWhiteSpace(row.Details) ? row.Action : $"{row.Action}: {row.Details}",
        Details = row.Details
    };

    private static bool HasOverlap(TimeOnly firstStart, TimeOnly firstEnd, TimeOnly secondStart, TimeOnly secondEnd) =>
        firstStart < secondEnd && firstEnd > secondStart;

    private static int GetMinutes(TimeOnly start, TimeOnly end) =>
        (int) (end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes;

    private static List<(TimeOnly Start, TimeOnly End)> GetFreeSegments(
        TimeOnly requestedStart,
        TimeOnly requestedEnd,
        List<(TimeOnly Start, TimeOnly End)> blockingIntervals)
    {
        var freeSegments = new List<(TimeOnly Start, TimeOnly End)> { (requestedStart, requestedEnd) };
        foreach (var block in blockingIntervals
            .Where(interval => HasOverlap(interval.Start, interval.End, requestedStart, requestedEnd))
            .OrderBy(interval => interval.Start))
        {
            var next = new List<(TimeOnly Start, TimeOnly End)>();
            foreach (var segment in freeSegments)
            {
                if (!HasOverlap(segment.Start, segment.End, block.Start, block.End))
                {
                    next.Add(segment);
                    continue;
                }

                if (block.Start > segment.Start)
                {
                    next.Add((segment.Start, block.Start));
                }
                if (block.End < segment.End)
                {
                    next.Add((block.End, segment.End));
                }
            }

            freeSegments = next;
            if (freeSegments.Count == 0)
            {
                break;
            }
        }

        return freeSegments
            .Where(segment => segment.End > segment.Start)
            .ToList();
    }

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

    private static DateTime NormalizeDate(DateTimeOffset value)
    {
        var tz = GetInstitutionTimeZone();
        var localTime = TimeZoneInfo.ConvertTime(value, tz);
        return DateTime.SpecifyKind(localTime.Date, DateTimeKind.Utc);
    }

    private static DateTime CombineDateAndTime(DateTime date, TimeOnly time) =>
        DateTime.SpecifyKind(date.Date.Add(time.ToTimeSpan()), DateTimeKind.Utc);

    private static void EnsureSubstitutionWindowNotFinished(DateTime date, TimeOnly endTime, string message)
    {
        if (DateTime.UtcNow > CombineDateAndTime(date, endTime))
        {
            throw new UserFriendlyException(message);
        }
    }

    private static void EnsureSubstitutionWindowStarted(DateTime date, TimeOnly startTime, string message)
    {
        if (DateTime.UtcNow < CombineDateAndTime(date, startTime))
        {
            throw new UserFriendlyException(message);
        }
    }

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

    private static SubstitutionConflictStatus ParseConflict(string? value) =>
        value == "Hard Block" ? SubstitutionConflictStatus.HardBlock :
        value == "Warning" ? SubstitutionConflictStatus.Warning :
        value == "Info" ? SubstitutionConflictStatus.Info :
        SubstitutionConflictStatus.Clear;

    private static string FormatStatus(SubstitutionStatus status) => status switch
    {
        SubstitutionStatus.WaitingAck => "Waiting Ack",
        _ => status.ToString()
    };

    private static string FormatAck(SubstitutionAcknowledgementStatus status) => status switch
    {
        SubstitutionAcknowledgementStatus.NotSent => "Not Sent",
        SubstitutionAcknowledgementStatus.ChangeRequested => "Change Requested",
        _ => status.ToString()
    };

    private static string FormatSource(SubstitutionSource source) => source switch
    {
        SubstitutionSource.AutoCreated => "Auto Created",
        SubstitutionSource.HrLeave => "HR Leave",
        SubstitutionSource.AcademicCalendar => "Calendar",
        SubstitutionSource.ExamDuty => "Exam Duty",
        SubstitutionSource.Suggested => "AI Suggested",
        _ => source.ToString()
    };

    private static string FormatConflict(SubstitutionConflictStatus status) => status switch
    {
        SubstitutionConflictStatus.HardBlock => "Hard Block",
        _ => status.ToString()
    };

    private static List<string> BuildRiskAlerts(List<SubstitutionRequest> rows)
    {
        var alerts = new List<string>();
        var pending = rows.Count(x => x.Status == SubstitutionStatus.Pending);
        var conflicts = rows.Count(x => x.Status == SubstitutionStatus.Conflict);
        var waitingAck = rows.Count(x => x.Status == SubstitutionStatus.WaitingAck);
        if (pending > 0) alerts.Add($"{pending} substitution request(s) still need assignment.");
        if (waitingAck > 0) alerts.Add($"{waitingAck} duty assignment(s) are waiting for acknowledgement.");
        if (conflicts > 0) alerts.Add($"{conflicts} substitution request(s) have hard conflicts.");
        return alerts;
    }

    private async Task<List<SuggestedTeacherDto>> BuildWorkloadSnapshotAsync(DateTime date, Guid? academicMediumId)
    {
        var contextTeacherUserIds = await GetTeacherUserIdsForAcademicMediumAsync(academicMediumId);
        var employees = await employeeRepository.GetListAsync(x =>
            !x.EmployeeStatusType.HasValue || x.EmployeeStatusType == EmployeeStatusType.Active);
        HashSet<Guid>? visibleTeacherIds = null;
        if (!IsAdminUser() && CurrentUser.Id.HasValue)
        {
            visibleTeacherIds = await GetVisibleTeacherReferenceIdsAsync(CurrentUser.Id.Value);
            employees = employees
                .Where(x => visibleTeacherIds.Contains(x.Id) ||
                            (x.UserId.HasValue && visibleTeacherIds.Contains(x.UserId.Value)))
                .ToList();
        }
        if (academicMediumId.HasValue && academicMediumId.Value != Guid.Empty)
        {
            employees = employees
                .Where(x =>
                    contextTeacherUserIds.Contains(x.Id) ||
                    (x.UserId.HasValue && contextTeacherUserIds.Contains(x.UserId.Value)))
                .ToList();
        }

        var rows = await FilterByAcademicMediumAsync(await substitutionRepository.GetListAsync(x => x.Date == date), academicMediumId);
        if (visibleTeacherIds != null)
        {
            rows = rows.Where(x =>
                (x.OriginalTeacherId.HasValue && visibleTeacherIds.Contains(x.OriginalTeacherId.Value)) ||
                (x.SubstituteTeacherId.HasValue && visibleTeacherIds.Contains(x.SubstituteTeacherId.Value))).ToList();
        }
        return employees
            .Select(employee =>
            {
                var count = rows.Count(x => x.SubstituteTeacherId == employee.Id);
                return new SuggestedTeacherDto
                {
                    TeacherId = employee.Id,
                    TeacherName = employee.EmployeeName,
                    DepartmentName = employee.Department ?? string.Empty,
                    TodaySubstitutionCount = count,
                    WeekSubstitutionCount = count,
                    MonthSubstitutionCount = count,
                    LoadStatus = count >= 3 ? "High" : count >= 1 ? "Medium" : "Low",
                    Score = Math.Max(0, 100 - count * 15),
                    FreePeriods = Math.Max(0, 6 - count),
                    RecommendationReason = "Current substitution workload snapshot."
                };
            })
            .OrderByDescending(x => x.TodaySubstitutionCount)
            .ThenBy(x => x.TeacherName)
            .Take(10)
            .ToList();
    }

    private static List<SubstitutionAnalyticsBreakdownDto> BuildBreakdown(
        IEnumerable<SubstitutionRequest> rows,
        Func<SubstitutionRequest, string> selector)
    {
        return rows
            .Select(selector)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Take(10)
            .Select(x => new SubstitutionAnalyticsBreakdownDto { Label = x.Key, Count = x.Count() })
            .ToList();
    }

    private static string EscapeCsv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private static SubstitutionSettingsDto MapSettings(SubstitutionSetting entity) => new()
    {
        AcademicYearId = entity.AcademicYearId,
        MaxSubstitutionsPerTeacherPerDay = entity.MaxSubstitutionsPerTeacherPerDay,
        MaxSubstitutionsPerTeacherPerWeek = entity.MaxSubstitutionsPerTeacherPerWeek,
        AcknowledgementRequiredByDefault = entity.AcknowledgementRequiredByDefault,
        AutoReminderEnabled = entity.AutoReminderEnabled,
        ReminderBeforeMinutes = entity.ReminderBeforeMinutes,
        NotifyLeadershipForUrgent = entity.NotifyLeadershipForUrgent,
        AllowTeacherReject = entity.AllowTeacherReject,
        StudentVisibleDailySheet = entity.StudentVisibleDailySheet
    };

    private bool IsAdminUser()
    {
        return CurrentUser.Roles.Any(role =>
            string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase));
    }

    private Task EnsureAdminAsync(string action)
    {
        if (!IsAdminUser())
        {
            throw new UserFriendlyException($"Only administrators can {action}.");
        }
        return Task.CompletedTask;
    }

    private async Task EnsureCanViewSubstitutionAsync(SubstitutionRequest entity)
    {
        if (IsAdminUser() || !CurrentUser.Id.HasValue)
        {
            return;
        }

        var visibleTeacherIds = await GetVisibleTeacherReferenceIdsAsync(CurrentUser.Id.Value);
        var canView = (entity.OriginalTeacherId.HasValue && visibleTeacherIds.Contains(entity.OriginalTeacherId.Value)) ||
                      (entity.SubstituteTeacherId.HasValue && visibleTeacherIds.Contains(entity.SubstituteTeacherId.Value));
        if (!canView)
        {
            throw new Volo.Abp.Authorization.AbpAuthorizationException(
                "You are not authorized to view this substitution.");
        }
    }

    private async Task<HashSet<Guid>> GetVisibleTeacherReferenceIdsAsync(Guid currentUserId)
    {
        var employees = await employeeRepository.GetListAsync();
        var currentEmployee = employees.FirstOrDefault(x => x.UserId == currentUserId);
        var visibleTeacherIds = new HashSet<Guid> { currentUserId };
        if (currentEmployee == null)
        {
            return visibleTeacherIds;
        }

        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(currentEmployee.Id);
        while (queue.Count > 0)
        {
            var managerId = queue.Dequeue();
            if (!visited.Add(managerId))
            {
                continue;
            }

            var manager = employees.First(x => x.Id == managerId);
            visibleTeacherIds.Add(manager.Id);
            if (manager.UserId.HasValue)
            {
                visibleTeacherIds.Add(manager.UserId.Value);
            }

            foreach (var employee in employees.Where(x => x.ReportingManagerId == managerId))
            {
                queue.Enqueue(employee.Id);
            }
        }

        return visibleTeacherIds;
    }

    private async Task EnsureCanActAsSubstituteAsync(SubstitutionRequest entity)
    {
        if (IsAdminUser()) return;

        var currentUserId = CurrentUser.Id;
        if (!currentUserId.HasValue) throw new UserFriendlyException("Unauthorized.");
        
        var currentEmployee = await employeeRepository.FirstOrDefaultAsync(x => x.UserId == currentUserId.Value);
        if (currentEmployee == null) throw new UserFriendlyException("Employee record not found.");

        if (entity.SubstituteTeacherId != currentEmployee.Id)
        {
            throw new UserFriendlyException("Only the assigned substitute teacher or administrators can perform this action.");
        }
    }
}


internal static class DoubleExtensions
{
    public static decimal ToDecimalSafe(this double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? 0 : Convert.ToDecimal(value);
}
