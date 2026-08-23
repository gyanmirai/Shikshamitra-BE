using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.Academic.Timetable.Timetables;

[RemoteService(IsEnabled = false)]
[Authorize]
public class TimetableAppService(
    ITimetableRepository timetableRepository,
    TimetableManager timetableManager,
    ILectureSlotConfigRepository lectureSlotConfigRepository,
    IWeeklyLectureSlotRepository weeklyLectureSlotRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<ElectiveGroup, Guid> electiveGroupRepository,
    IRepository<Room, Guid> roomRepository,
    IRepository<AssignedSubject, Guid> assignedSubjectRepository,
    IRepository<Student, Guid> studentRepository,
    IRepository<StudentElectiveSelection, Guid> studentElectiveSelectionRepository,
    TimetableReportPdfService timetableReportPdfService,
    ITenantRepository tenantRepository,
    IRepository<Employee, Guid> employeeRepository,
    IUserScopeAccessService userScopeAccessService
) : ApplicationService, ITimetableAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<TimetableResponseDto> GetByStandardAndDivisionAsync(Guid standardId, Guid divisionId)
    {
        await EnsureCurrentUserCanAccessAsync(standardId, divisionId, "timetable-read");
        var timetable = await timetableRepository.FindByStandardAndDivisionAsync(standardId, divisionId);
        var slotConfig = await lectureSlotConfigRepository.FindByStandardIdAsync(standardId);

        var savedSlotsByDay = timetable?.Slots
            .GroupBy(s => s.Day)
            .ToDictionary(g => g.Key, g => g.ToList())
            ?? new Dictionary<TimetableDayOfWeek, List<TimetableSlot>>();

        var hasSavedData = savedSlotsByDay.Any(kv => kv.Value.Count > 0);

        // Weekly lecture slot is needed for totalSlots in subject summary
        var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(standardId);

        var assignedSubjects = await assignedSubjectRepository.GetListAsync(a =>
            a.StandardId == standardId &&
            a.Status == SubjectStatus.Published);
        var publishedWeeklySubjects = GetPublishedWeeklyLectureSubjects(weeklySlot, assignedSubjects);
        var assignedSubjectBySubjectId = assignedSubjects
            .GroupBy(a => a.SubjectId)
            .ToDictionary(g => g.Key, g => g.First().Id);
        var selectedElectiveSubjectIdsByGroup = await GetSelectedElectiveSubjectIdsByGroupAsync(standardId, divisionId);
        var needsSubjectData = hasSavedData || assignedSubjects.Count > 0;

        var subjects = needsSubjectData ? await subjectRepository.GetListAsync() : [];
        var electiveGroups = needsSubjectData ? await electiveGroupRepository.GetListAsync() : [];
        var rooms = hasSavedData ? await roomRepository.GetListAsync() : [];

        var teacherIds = hasSavedData
            ? timetable!.Slots.SelectMany(s => s.Subjects).Select(sub => sub.TeacherId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList()
            : new List<Guid>();
        var employees = teacherIds.Count > 0
            ? await employeeRepository.GetListAsync(e => teacherIds.Contains(e.Id))
            : new List<Employee>();

        // Compute occupied slots per subject across all days in this timetable
        var occupiedBySubject = hasSavedData
            ? timetable!.Slots.SelectMany(s => s.Subjects).GroupBy(ss => ss.SubjectId)
                .ToDictionary(g => g.Key, g => g.Count())
            : new Dictionary<Guid, int>();
        var occupiedByElective = hasSavedData
            ? timetable!.Slots
                .Where(s => s.ElectiveGroupId.HasValue)
                .GroupBy(s => s.ElectiveGroupId!.Value)
                .ToDictionary(g => g.Key, g => g.Count())
            : new Dictionary<Guid, int>();

        // For slot-level subject TotalSlots/OccupiedSlots display (existing per-slot logic)
        var totalSlotsBySubject = publishedWeeklySubjects
            .Where(s => s.SubjectId.HasValue)
            .ToDictionary(s => s.SubjectId!.Value, s => s.WeeklyLectures);
        var totalSlotsByElective = publishedWeeklySubjects
            .Where(s => s.ElectiveGroupId.HasValue)
            .ToDictionary(s => s.ElectiveGroupId!.Value, s => s.WeeklyLectures);

        // Build a (day, startTime, endTime) → (SlotType, BreakType) lookup from the slot config.
        // Must be keyed per-day: different day groups can share the same time slot with different
        // types (e.g. Mon-Fri has 09:00-09:30 as BREAK, Saturday has 08:00-08:30 as BREAK).
        // A flat (startTime, endTime) key causes Saturday's break to override Mon-Fri's lecture
        // at the same time slot whenever the groups happen to be iterated in the wrong order.
        var slotTypeByDayAndTime = new Dictionary<(TimetableDayOfWeek, TimeOnly, TimeOnly), (SlotType, BreakType?)>();
        if (slotConfig != null)
        {
            foreach (var group in slotConfig.SlotGroups)
            {
                for (var dayIdx = group.WeekRangeStart; dayIdx <= group.WeekRangeEnd; dayIdx++)
                {
                    var groupDay = (TimetableDayOfWeek) dayIdx;
                    foreach (var ts in group.TimeSlots)
                        slotTypeByDayAndTime.TryAdd((groupDay, ts.StartTime, ts.EndTime), (ts.SlotType, ts.BreakType));
                }
            }
        }

        var response = new TimetableResponseDto();

        // Always return all 6 days. Days with saved data use saved data;
        // days without saved data use slot config time slots as placeholders (empty subjects).
        foreach (var day in Enum.GetValues<TimetableDayOfWeek>())
        {
            var dayName = day.ToString().ToUpperInvariant();
            var dayIndex = (int)day;

            var slotGroup = slotConfig?.SlotGroups
                .FirstOrDefault(g => g.WeekRangeStart <= dayIndex && dayIndex <= g.WeekRangeEnd);
            var configTimeSlots = slotGroup?.TimeSlots.OrderBy(ts => ts.StartTime).ToList();

            if (configTimeSlots != null && configTimeSlots.Count > 0)
            {
                response.Days[dayName] = configTimeSlots.Select((ts, index) =>
                {
                    var matchingSavedSlot = savedSlotsByDay.TryGetValue(day, out var savedSlots)
                        ? savedSlots.FirstOrDefault(s => s.StartTime == ts.StartTime && s.EndTime == ts.EndTime)
                        : null;

                    if (matchingSavedSlot != null)
                    {
                        var resolvedSubjects = BuildSlotSubjectResponses(
                            matchingSavedSlot,
                            subjects,
                            rooms,
                            employees,
                            assignedSubjectBySubjectId,
                            totalSlotsBySubject,
                            totalSlotsByElective,
                            occupiedBySubject,
                            occupiedByElective,
                            selectedElectiveSubjectIdsByGroup);

                        return new TimetableSlotResponseDto
                        {
                            Sequence = index + 1,
                            StartTime = ts.StartTime.ToString("HH:mm"),
                            EndTime = ts.EndTime.ToString("HH:mm"),
                            Type = ts.SlotType,
                            BreakType = ts.BreakType,
                            SubjectType = matchingSavedSlot.SubjectType,
                            ElectiveGroupId = matchingSavedSlot.ElectiveGroupId,
                            ElectiveGroupName = matchingSavedSlot.ElectiveGroupId.HasValue
                                ? (resolvedSubjects.Count == 1 ? resolvedSubjects[0].Name : electiveGroups.FirstOrDefault(eg => eg.Id == matchingSavedSlot.ElectiveGroupId.Value)?.Name)
                                : null,
                            ElectiveGroupColor = matchingSavedSlot.ElectiveGroupId.HasValue
                                ? electiveGroups.FirstOrDefault(eg => eg.Id == matchingSavedSlot.ElectiveGroupId.Value)?.Color
                                : null,
                            Subjects = resolvedSubjects
                        };
                    }
                    else
                    {
                        return new TimetableSlotResponseDto
                        {
                            Sequence = index + 1,
                            StartTime = ts.StartTime.ToString("HH:mm"),
                            EndTime = ts.EndTime.ToString("HH:mm"),
                            Type = ts.SlotType,
                            BreakType = ts.BreakType,
                            SubjectType = null,
                            ElectiveGroupId = null,
                            ElectiveGroupName = null,
                            Subjects = []
                        };
                    }
                }).ToList();
            }
            else if (savedSlotsByDay.TryGetValue(day, out var savedSlots) && savedSlots.Count > 0)
            {
                response.Days[dayName] = savedSlots.OrderBy(s => s.Sequence).Select(slot =>
                {
                    var (resolvedType, resolvedBreakType) = slotTypeByDayAndTime.TryGetValue(
                        (slot.Day, slot.StartTime, slot.EndTime), out var configType)
                        ? configType
                        : (slot.SlotType, slot.BreakType);
                    var resolvedSubjects = BuildSlotSubjectResponses(
                        slot,
                        subjects,
                        rooms,
                        employees,
                        assignedSubjectBySubjectId,
                        totalSlotsBySubject,
                        totalSlotsByElective,
                        occupiedBySubject,
                        occupiedByElective,
                        selectedElectiveSubjectIdsByGroup);

                    return new TimetableSlotResponseDto
                    {
                        Sequence = slot.Sequence,
                        StartTime = slot.StartTime.ToString("HH:mm"),
                        EndTime = slot.EndTime.ToString("HH:mm"),
                        Type = resolvedType,
                        BreakType = resolvedBreakType,
                        SubjectType = slot.SubjectType,
                        ElectiveGroupId = slot.ElectiveGroupId,
                        ElectiveGroupName = slot.ElectiveGroupId.HasValue
                        ? (resolvedSubjects.Count == 1 ? resolvedSubjects[0].Name : electiveGroups.FirstOrDefault(eg => eg.Id == slot.ElectiveGroupId.Value)?.Name)
                        : null,
                        ElectiveGroupColor = slot.ElectiveGroupId.HasValue
                        ? electiveGroups.FirstOrDefault(eg => eg.Id == slot.ElectiveGroupId.Value)?.Color
                        : null,
                        Subjects = resolvedSubjects
                    };
                }).ToList();
            }
            else
            {
                response.Days[dayName] = [];
            }
        }

        // Compute overall slot summary across all days
        var totalSlots = response.Days.Values.Sum(slots => slots.Count);
        var occupiedSlots = response.Days.Values.Sum(slots => slots.Count(s => s.Subjects.Count > 0));
        response.TotalSlots = totalSlots;
        response.OccupiedSlots = occupiedSlots;
        response.UnoccupiedSlots = totalSlots - occupiedSlots;

        // Build subject summary from assigned subjects (always populated, even if no weekly slot plan).
        // totalSlots comes from weeklySlot when configured; otherwise 0.
        // occupiedSlots = 0 when no timetable saved yet → unoccupiedSlots = totalSlots.
        if (assignedSubjects.Count > 0)
        {
            var coreOccupied = timetable?.Slots
                .Where(s => s.ElectiveGroupId == null)
                .SelectMany(s => s.Subjects)
                .GroupBy(ss => ss.SubjectId)
                .ToDictionary(g => g.Key, g => g.Count())
                ?? new Dictionary<Guid, int>();

            var electiveOccupied = timetable?.Slots
                .Where(s => s.ElectiveGroupId.HasValue)
                .GroupBy(s => s.ElectiveGroupId!.Value)
                .ToDictionary(g => g.Key, g => g.Count())
                ?? new Dictionary<Guid, int>();

            // Core subjects: one entry per distinct subjectId with no electiveGroupId
            var coreEntries = assignedSubjects
                .Where(a => a.ElectiveGroupId == null)
                .GroupBy(a => a.SubjectId)
                .Select(g =>
                {
                    var assigned = g.First();
                    var subject = subjects.FirstOrDefault(s => s.Id == assigned.SubjectId);
                    var weeklyEntry = publishedWeeklySubjects.FirstOrDefault(w => w.SubjectId == assigned.SubjectId);
                    var total = weeklyEntry?.WeeklyLectures ?? 0;
                    var occupied = coreOccupied.GetValueOrDefault(assigned.SubjectId, 0);
                    return new TimetableSubjectSummaryDto
                    {
                        Id = weeklyEntry?.Id ?? assigned.Id,
                        SubjectAssignmentId = assigned.Id,
                        SubjectType = SubjectType.Core,
                        ElectiveGroupId = null,
                        ElectiveGroupName = null,
                        Color = subject?.Color,
                        TotalSlots = total,
                        OccupiedSlots = occupied,
                        UnoccupiedSlots = total - occupied,
                        Subjects =
                        [
                            new TimetableSummarySubjectDto
                            {
                                SubjectAssignmentId = assigned.Id,
                                SubjectType = SubjectType.Core,
                                SubjectId = assigned.SubjectId,
                                SubjectName = subject?.Name ?? string.Empty,
                                SubjectCode = assigned.SubjectCode,
                                Color = subject?.Color
                            }
                        ]
                    };
                }).ToList();

            // Elective subjects: one entry per distinct electiveGroupId.
            // When student elective selections exist for this division, filter to only the
            // subjects that students actually selected — so the timetable palette and grid
            // show "Pt" instead of "Computer | Pt" when 100% of students chose Pt.
            var electiveEntries = assignedSubjects
                .Where(a => a.ElectiveGroupId.HasValue)
                .GroupBy(a => a.ElectiveGroupId!.Value)
                .Select(g =>
                {
                    var groupId = g.Key;
                    var group = electiveGroups.FirstOrDefault(eg => eg.Id == groupId);
                    var weeklyEntry = publishedWeeklySubjects.FirstOrDefault(w => w.ElectiveGroupId == groupId);
                    var total = weeklyEntry?.WeeklyLectures ?? 0;
                    var occupied = electiveOccupied.GetValueOrDefault(groupId, 0);

                    // Use per-division student selections when available; fall back to all assigned subjects.
                    var selectedForGroup = selectedElectiveSubjectIdsByGroup
                        .GetValueOrDefault(groupId, g.Select(a => a.SubjectId).ToList());

                    var groupSubjects = g
                        .Where(a => selectedForGroup.Contains(a.SubjectId))
                        .Select(a =>
                        {
                            var subject = subjects.FirstOrDefault(s => s.Id == a.SubjectId);
                            return new TimetableSummarySubjectDto
                            {
                                SubjectAssignmentId = a.Id,
                                SubjectType = SubjectType.Elective,
                                SubjectId = a.SubjectId,
                                SubjectName = subject?.Name ?? string.Empty,
                                SubjectCode = a.SubjectCode,
                                Color = subject?.Color
                            };
                        }).ToList();

                    // When only one subject remains after filtering, use its name as the
                    // display name so the palette shows "Pt" instead of the group name.
                    var displayName = groupSubjects.Count == 1
                        ? groupSubjects[0].SubjectName
                        : group?.Name;

                    return new TimetableSubjectSummaryDto
                    {
                        Id = weeklyEntry?.Id ?? groupId,
                        SubjectAssignmentId = g.First().Id,
                        SubjectType = SubjectType.Elective,
                        ElectiveGroupId = groupId,
                        ElectiveGroupName = displayName,
                        Color = group?.Color,
                        TotalSlots = total,
                        OccupiedSlots = occupied,
                        UnoccupiedSlots = total - occupied,
                        Subjects = groupSubjects
                    };
                }).ToList();

            response.SubjectSummary = [.. coreEntries, .. electiveEntries];
        }

        return response;
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<EmployeeTimetableResponseDto> GetByEmployeeAsync(Guid employeeId, Guid? academicYearId = null)
    {
        var employee = await employeeRepository.FindAsync(employeeId);
        var timetables = await timetableRepository.GetByEmployeeIdAsync(employeeId, academicYearId);
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        timetables = timetables.FilterByUserScope(scope).ToList();

        var standardIds = timetables.Select(t => t.StandardId).Distinct().ToList();
        var divisionIds = timetables.Select(t => t.DivisionId).Distinct().ToList();
        var subjectIds = timetables
            .SelectMany(t => t.Slots)
            .SelectMany(s => s.Subjects)
            .Where(s => s.TeacherId == employeeId)
            .Select(s => s.SubjectId)
            .Distinct()
            .ToList();
        var roomIds = timetables
            .SelectMany(t => t.Slots)
            .SelectMany(s => s.Subjects)
            .Where(s => s.TeacherId == employeeId && s.RoomId.HasValue)
            .Select(s => s.RoomId!.Value)
            .Distinct()
            .ToList();
        var electiveGroupIds = timetables
            .SelectMany(t => t.Slots)
            .Where(s => s.ElectiveGroupId.HasValue && s.Subjects.Any(subject => subject.TeacherId == employeeId))
            .Select(s => s.ElectiveGroupId!.Value)
            .Distinct()
            .ToList();

        var standards = standardIds.Count > 0
            ? await standardRepository.GetListAsync(s => standardIds.Contains(s.Id))
            : [];
        var divisions = divisionIds.Count > 0
            ? await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id))
            : [];
        var subjects = subjectIds.Count > 0
            ? await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id))
            : [];
        var rooms = roomIds.Count > 0
            ? await roomRepository.GetListAsync(r => roomIds.Contains(r.Id))
            : [];
        var electiveGroups = electiveGroupIds.Count > 0
            ? await electiveGroupRepository.GetListAsync(g => electiveGroupIds.Contains(g.Id))
            : [];
        var assignedSubjects = subjectIds.Count > 0 && standardIds.Count > 0
            ? await assignedSubjectRepository.GetListAsync(a =>
                standardIds.Contains(a.StandardId) &&
                subjectIds.Contains(a.SubjectId) &&
                a.Status == SubjectStatus.Published)
            : [];

        var response = new EmployeeTimetableResponseDto
        {
            EmployeeId = employeeId,
            EmployeeName = employee?.EmployeeName,
            AcademicYearId = academicYearId
        };

        foreach (var day in Enum.GetValues<TimetableDayOfWeek>())
        {
            var dayName = day.ToString().ToUpperInvariant();
            response.Days[dayName] = [];
        }

        foreach (var timetable in timetables)
        {
            var standard = standards.FirstOrDefault(s => s.Id == timetable.StandardId);
            var division = divisions.FirstOrDefault(d => d.Id == timetable.DivisionId);

            foreach (var slot in timetable.Slots
                .Where(s => s.Subjects.Any(subject => subject.TeacherId == employeeId))
                .OrderBy(s => s.Day)
                .ThenBy(s => s.Sequence)
                .ThenBy(s => s.StartTime))
            {
                var dayName = slot.Day.ToString().ToUpperInvariant();
                var electiveGroup = slot.ElectiveGroupId.HasValue
                    ? electiveGroups.FirstOrDefault(g => g.Id == slot.ElectiveGroupId.Value)
                    : null;

                foreach (var slotSubject in slot.Subjects.Where(s => s.TeacherId == employeeId))
                {
                    var subject = subjects.FirstOrDefault(s => s.Id == slotSubject.SubjectId);
                    var room = slotSubject.RoomId.HasValue
                        ? rooms.FirstOrDefault(r => r.Id == slotSubject.RoomId.Value)
                        : null;
                    var assignedSubject = assignedSubjects.FirstOrDefault(a =>
                        a.StandardId == timetable.StandardId &&
                        a.SubjectId == slotSubject.SubjectId);

                    response.Days[dayName].Add(new EmployeeTimetableSlotResponseDto
                    {
                        TimetableId = timetable.Id,
                        TimetableSlotId = slot.Id,
                        StandardId = timetable.StandardId,
                        StandardName = standard?.Name ?? string.Empty,
                        DivisionId = timetable.DivisionId,
                        DivisionName = division?.Name ?? string.Empty,
                        Sequence = slot.Sequence,
                        StartTime = slot.StartTime.ToString("HH:mm"),
                        EndTime = slot.EndTime.ToString("HH:mm"),
                        Type = slot.SlotType,
                        BreakType = slot.BreakType,
                        SubjectType = slot.SubjectType,
                        ElectiveGroupId = slot.ElectiveGroupId,
                        ElectiveGroupName = electiveGroup?.Name,
                        ElectiveGroupColor = electiveGroup?.Color,
                        SubjectAssignmentId = assignedSubject?.Id ?? Guid.Empty,
                        SubjectId = slotSubject.SubjectId,
                        SubjectName = subject?.Name ?? string.Empty,
                        SubjectCode = assignedSubject?.SubjectCode,
                        SubjectColor = subject?.Color,
                        RoomId = slotSubject.RoomId,
                        RoomName = room?.RoomName
                    });
                }
            }
        }

        foreach (var daySlots in response.Days.Values)
        {
            daySlots.Sort((left, right) =>
            {
                var timeCompare = string.Compare(left.StartTime, right.StartTime, StringComparison.Ordinal);
                if (timeCompare != 0)
                {
                    return timeCompare;
                }

                var sequenceCompare = left.Sequence.CompareTo(right.Sequence);
                if (sequenceCompare != 0)
                {
                    return sequenceCompare;
                }

                return string.Compare(left.StandardName, right.StandardName, StringComparison.Ordinal);
            });
        }

        response.TotalSlots = response.Days.Values.Sum(slots => slots.Count);
        return response;
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public async Task SaveAsync(Guid standardId, Guid divisionId, SaveTimetableDto input)
    {
        await EnsureCurrentUserCanAccessAsync(standardId, divisionId, "timetable-save");
        var timetable = await timetableRepository.FindByStandardAndDivisionAsync(standardId, divisionId);

        if (timetable is null)
        {
            timetable = new Timetable(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                standardId,
                divisionId);
        }

        ReplaceSlotsFromInput(timetable, input);

        await EnsureNoTeacherOrRoomConflictsAsync(timetable);

        if (input.Status == TimetableStatus.Published)
        {
            await EnsureTimetableReadyToPublishAsync(timetable);
            timetable.Publish();
        }
        else
        {
            timetable.SaveAsDraft();
        }

        if (timetable.Id == Guid.Empty || await timetableRepository.FindAsync(timetable.Id) is null)
        {
            await timetableRepository.InsertAsync(timetable);
        }
        else
        {
            await timetableRepository.UpdateAsync(timetable);
        }
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public async Task ValidateAsync(Guid standardId, Guid divisionId, SaveTimetableDto input)
    {
        await EnsureCurrentUserCanAccessAsync(standardId, divisionId, "timetable-validate");
        var timetable = new Timetable(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            standardId,
            divisionId);

        ReplaceSlotsFromInput(timetable, input);
        await EnsureNoTeacherOrRoomConflictsAsync(timetable);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<TimetableManagementResponseDto> GetManagementByStandardAsync(
        Guid standardId, GetTimetableManagementInput input)
    {
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        var lectureSlotConfig = await lectureSlotConfigRepository.FindByStandardIdAsync(standardId);

        var standard = await standardRepository.GetAsync(standardId);
        var divisionQueryable = await divisionRepository.GetQueryableAsync();
        var divisions = await AsyncExecuter.ToListAsync(
            divisionQueryable.Where(d => d.StandardId == standardId).OrderBy(d => d.Name));

        if (!scope.IsAdmin && !scope.HasTenantWideScope())
        {
            divisions = divisions
                .Where(division => UserScopeQueryExtensions.HasAccessToEntity(
                    new Timetable(Guid.Empty, CurrentTenant.Id, standardId, division.Id), scope))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            divisions = divisions
                .Where(d => d.Name.Contains(input.Search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalCount = divisions.Count;
        var pagedDivisions = divisions
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var timetables = await timetableRepository.GetByStandardIdAsync(standardId);
        var allTimetables = await timetableRepository.GetAllWithSlotsAsync();
        var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(standardId);
        var assignedSubjectsQueryable = await assignedSubjectRepository.WithDetailsAsync(a => a.Teachers);
        var assignedSubjects = await AsyncExecuter.ToListAsync(
            assignedSubjectsQueryable.Where(a =>
                a.StandardId == standardId &&
                a.Status == SubjectStatus.Published));
        var publishedWeeklySubjects = GetPublishedWeeklyLectureSubjects(weeklySlot, assignedSubjects);
        var roomsCount = await roomRepository.GetCountAsync();

        int weeklySlots = 0;
        if (lectureSlotConfig != null)
        {
            foreach (var group in lectureSlotConfig.SlotGroups)
            {
                int numDays = group.WeekRangeEnd - group.WeekRangeStart + 1;
                int lectureSlotsInGroup = group.TimeSlots.Count(ts => ts.SlotType == SlotType.Period);
                weeklySlots += numDays * lectureSlotsInGroup;
            }
        }

        int totalRequiredWeeklyLectures = publishedWeeklySubjects.Sum(s => s.WeeklyLectures);
        int subjectsWithoutTeachersCount = 0;
        if (publishedWeeklySubjects.Count > 0)
        {
            var weeklySubjectIds = publishedWeeklySubjects
                .Where(s => s.SubjectId.HasValue && s.WeeklyLectures > 0)
                .Select(s => s.SubjectId!.Value)
                .ToHashSet();

            var electiveGroupIds = publishedWeeklySubjects
                .Where(s => s.ElectiveGroupId.HasValue && s.WeeklyLectures > 0)
                .Select(s => s.ElectiveGroupId!.Value)
                .ToHashSet();

            subjectsWithoutTeachersCount = assignedSubjects.Count(a =>
                ((a.SubjectType == SubjectType.Core && weeklySubjectIds.Contains(a.SubjectId)) ||
                 (a.SubjectType == SubjectType.Elective && a.ElectiveGroupId.HasValue && electiveGroupIds.Contains(a.ElectiveGroupId.Value))) &&
                (a.Teachers == null || a.Teachers.Count == 0));
        }
        else
        {
            subjectsWithoutTeachersCount = assignedSubjects.Count(a => a.Teachers == null || a.Teachers.Count == 0);
        }
        bool hasRooms = roomsCount > 0;

        var items = pagedDivisions.Select(division =>
        {
            var tt = timetables.FirstOrDefault(t => t.DivisionId == division.Id);
            
            int filledSlots = tt?.Slots.Count(s => s.SlotType == SlotType.Period && s.Subjects.Count > 0) ?? 0;
            int subjectCompletedCount = 0;
            int subjectTotalCount = publishedWeeklySubjects.Count;
            int teacherAssignedCount = tt?.Slots.Where(s => s.SlotType == SlotType.Period).SelectMany(s => s.Subjects).Count(sub => sub.TeacherId.HasValue) ?? 0;
            int teacherTotalCount = tt?.Slots.Where(s => s.SlotType == SlotType.Period).SelectMany(s => s.Subjects).Count() ?? 0;
            int roomAssignedCount = tt?.Slots.Where(s => s.SlotType == SlotType.Period).SelectMany(s => s.Subjects).Count(sub => sub.RoomId.HasValue) ?? 0;
            int roomTotalCount = tt?.Slots.Where(s => s.SlotType == SlotType.Period).SelectMany(s => s.Subjects).Count() ?? 0;
            int conflictCount = 0;

            if (tt != null && publishedWeeklySubjects.Count > 0)
            {
                var coreOccupied = tt.Slots
                    .Where(s => s.ElectiveGroupId == null)
                    .SelectMany(s => s.Subjects)
                    .GroupBy(ss => ss.SubjectId)
                    .ToDictionary(g => g.Key, g => g.Count());

                var electiveOccupied = tt.Slots
                    .Where(s => s.ElectiveGroupId.HasValue)
                    .GroupBy(s => s.ElectiveGroupId!.Value)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var req in publishedWeeklySubjects)
                {
                    int assigned = 0;
                    if (req.ElectiveGroupId.HasValue)
                    {
                        assigned = electiveOccupied.GetValueOrDefault(req.ElectiveGroupId.Value, 0);
                    }
                    else if (req.SubjectId.HasValue)
                    {
                        assigned = coreOccupied.GetValueOrDefault(req.SubjectId.Value, 0);
                    }

                    if (assigned == req.WeeklyLectures)
                    {
                        subjectCompletedCount++;
                    }
                }
            }

            if (tt != null)
            {
                // 1. Same-slot conflicts
                foreach (var slot in tt.Slots.Where(s => s.SlotType == SlotType.Period))
                {
                    var slotTeachers = slot.Subjects.Where(sub => sub.TeacherId.HasValue).Select(sub => sub.TeacherId!.Value).ToList();
                    if (slotTeachers.Count != slotTeachers.Distinct().Count())
                    {
                        conflictCount += (slotTeachers.Count - slotTeachers.Distinct().Count());
                    }

                    var slotRooms = slot.Subjects.Where(sub => sub.RoomId.HasValue).Select(sub => sub.RoomId!.Value).ToList();
                    if (slotRooms.Count != slotRooms.Distinct().Count())
                    {
                        conflictCount += (slotRooms.Count - slotRooms.Distinct().Count());
                    }
                }

                // 2. Cross-class/division conflicts with any active timetable.
                var otherTimetables = allTimetables
                    .Where(t =>
                        (t.StandardId != tt.StandardId || t.DivisionId != tt.DivisionId) &&
                        t.Status != TimetableStatus.NotCreated)
                    .ToList();

                foreach (var slot in tt.Slots.Where(s => s.SlotType == SlotType.Period))
                {
                    var teacherIds = slot.Subjects.Where(sub => sub.TeacherId.HasValue).Select(sub => sub.TeacherId!.Value).ToList();
                    var roomIds = slot.Subjects.Where(sub => sub.RoomId.HasValue).Select(sub => sub.RoomId!.Value).ToList();

                    if (teacherIds.Count > 0 || roomIds.Count > 0)
                    {
                        foreach (var otherTt in otherTimetables)
                        {
                            var otherSlot = otherTt.Slots.FirstOrDefault(s => s.Day == slot.Day && s.StartTime == slot.StartTime && s.EndTime == slot.EndTime);
                            if (otherSlot != null)
                            {
                                var otherTeacherIds = otherSlot.Subjects.Where(sub => sub.TeacherId.HasValue).Select(sub => sub.TeacherId!.Value);
                                var otherRoomIds = otherSlot.Subjects.Where(sub => sub.RoomId.HasValue).Select(sub => sub.RoomId!.Value);

                                conflictCount += teacherIds.Intersect(otherTeacherIds).Count();
                                conflictCount += roomIds.Intersect(otherRoomIds).Count();
                            }
                        }
                    }
                }

                // 3. Subject over-assignment conflicts
                if (publishedWeeklySubjects.Count > 0)
                {
                    var coreOccupied = tt.Slots
                        .Where(s => s.ElectiveGroupId == null)
                        .SelectMany(s => s.Subjects)
                        .GroupBy(ss => ss.SubjectId)
                        .ToDictionary(g => g.Key, g => g.Count());

                    var electiveOccupied = tt.Slots
                        .Where(s => s.ElectiveGroupId.HasValue)
                        .GroupBy(s => s.ElectiveGroupId!.Value)
                        .ToDictionary(g => g.Key, g => g.Count());

                    foreach (var req in publishedWeeklySubjects)
                    {
                        int assigned = 0;
                        if (req.ElectiveGroupId.HasValue)
                        {
                            assigned = electiveOccupied.GetValueOrDefault(req.ElectiveGroupId.Value, 0);
                        }
                        else if (req.SubjectId.HasValue)
                        {
                            assigned = coreOccupied.GetValueOrDefault(req.SubjectId.Value, 0);
                        }

                        if (assigned > req.WeeklyLectures)
                        {
                            conflictCount += (assigned - req.WeeklyLectures);
                        }
                    }
                }
            }

            var missingItems = new List<string>();
            if (lectureSlotConfig == null || weeklySlots == 0)
            {
                missingItems.Add("Lecture Slots not configured");
            }
            if (weeklySlot == null || publishedWeeklySubjects.Count == 0)
            {
                missingItems.Add("Weekly Lectures not configured");
            }
            if (assignedSubjects.Count == 0)
            {
                missingItems.Add("Subjects not assigned to standard");
            }
            if (subjectsWithoutTeachersCount > 0)
            {
                missingItems.Add($"{subjectsWithoutTeachersCount} subjects do not have teachers assigned");
            }
            if (!hasRooms)
            {
                missingItems.Add("Rooms not configured");
            }
            if (weeklySlots > 0 && totalRequiredWeeklyLectures > weeklySlots)
            {
                missingItems.Add("Available weekly slots are less than required weekly lectures (capacity mismatch)");
            }

            bool isReady = missingItems.Count == 0;
            int totalChecklistItems = 6;
            int readinessScore = (totalChecklistItems - missingItems.Count) * 100 / totalChecklistItems;
            if (readinessScore < 0) readinessScore = 0;

            return new DivisionTimetableStatusDto
            {
                DivisionId = division.Id,
                DivisionName = division.Name,
                Status = tt?.Status ?? TimetableStatus.NotCreated,
                WeeklySlots = weeklySlots,
                FilledSlots = filledSlots,
                SubjectCompletedCount = subjectCompletedCount,
                SubjectTotalCount = subjectTotalCount,
                TeacherAssignedCount = teacherAssignedCount,
                TeacherTotalCount = teacherTotalCount,
                RoomAssignedCount = roomAssignedCount,
                RoomTotalCount = roomTotalCount,
                ConflictCount = conflictCount,
                IsReady = isReady,
                ReadinessScore = readinessScore,
                MissingItems = missingItems
            };
        }).ToList();

        return new TimetableManagementResponseDto
        {
            StandardId = standardId,
            StandardName = standard.Name,
            Items = items,
            TotalCount = totalCount,
            TotalPages = (int) Math.Ceiling(totalCount / (double) input.MaxResultCount),
            CurrentPage = input.SkipCount / input.MaxResultCount + 1,
            PageSize = input.MaxResultCount
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public async Task AssignTeachersAsync(AssignTeachersDto input)
    {
        await EnsureCurrentUserCanAccessAsync(input.StandardId, input.DivisionId, "timetable-assign-teachers");
        var day = Enum.Parse<TimetableDayOfWeek>(input.Day, ignoreCase: true);
        var startTime = input.StartTime;
        var endTime = input.EndTime;

        var teacherIds = input.Subjects
            .Where(s => s.TeacherId.HasValue)
            .Select(s => s.TeacherId!.Value)
            .ToList();
        if (teacherIds.Count > 0)
        {
            await timetableManager.CheckTeacherConflictAsync(
                input.StandardId, input.DivisionId, day, startTime, endTime, teacherIds);
        }

        var timetable = await timetableRepository.FindByStandardAndDivisionAsync(
            input.StandardId, input.DivisionId)
            ?? throw new BusinessException(TimetableErrorCodes.TimetableNotFound);
        EnsureTimetableEditable(timetable);

        var slot = timetable.FindSlot(day, startTime, endTime)
            ?? throw new BusinessException(TimetableErrorCodes.TimetableSlotNotFound);

        foreach (var assignment in input.Subjects)
        {
            var subject = slot.Subjects.FirstOrDefault(s => s.SubjectId == assignment.SubjectId);
            subject?.AssignTeacher(assignment.TeacherId);
        }

        await timetableRepository.UpdateAsync(timetable);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public async Task AssignRoomsAsync(AssignRoomsDto input)
    {
        await EnsureCurrentUserCanAccessAsync(input.StandardId, input.DivisionId, "timetable-assign-rooms");
        var day = Enum.Parse<TimetableDayOfWeek>(input.Day, ignoreCase: true);
        var startTime = input.StartTime;
        var endTime = input.EndTime;

        var roomIds = input.Subjects
            .Where(s => s.RoomId.HasValue)
            .Select(s => s.RoomId!.Value)
            .ToList();
        if (roomIds.Count > 0)
        {
            await timetableManager.CheckRoomConflictAsync(
                input.StandardId, input.DivisionId, day, startTime, endTime, roomIds);
        }

        var timetable = await timetableRepository.FindByStandardAndDivisionAsync(
            input.StandardId, input.DivisionId)
            ?? throw new BusinessException(TimetableErrorCodes.TimetableNotFound);
        EnsureTimetableEditable(timetable);

        var slot = timetable.FindSlot(day, startTime, endTime)
            ?? throw new BusinessException(TimetableErrorCodes.TimetableSlotNotFound);

        foreach (var assignment in input.Subjects)
        {
            var subject = slot.Subjects.FirstOrDefault(s => s.SubjectId == assignment.SubjectId);
            subject?.AssignRoom(assignment.RoomId);
        }

        await timetableRepository.UpdateAsync(timetable);
    }

    private static void EnsureTimetableEditable(Timetable timetable)
    {
        if (timetable.Status == TimetableStatus.Published)
        {
            throw new BusinessException("Academic:Timetable:PublishedTimetableLocked")
                .WithData("StandardId", timetable.StandardId)
                .WithData("DivisionId", timetable.DivisionId)
                .WithData("message", "Published timetable cannot be edited directly. Create a new draft/change workflow before changing slots, teachers, or rooms.");
        }
    }

    private async Task EnsureTimetableReadyToPublishAsync(Timetable timetable)
    {
        var lectureSlotConfig = await lectureSlotConfigRepository.FindByStandardIdAsync(timetable.StandardId);
        if (lectureSlotConfig == null || lectureSlotConfig.SlotGroups.Count == 0)
        {
            throw new BusinessException(TimetableErrorCodes.LectureSlotConfigNotFound)
                .WithData("StandardId", timetable.StandardId);
        }

        var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(timetable.StandardId);
        if (weeklySlot == null || weeklySlot.Subjects.Count == 0)
        {
            throw new BusinessException(TimetableErrorCodes.WeeklyLectureSlotNotFound)
                .WithData("StandardId", timetable.StandardId);
        }
        var publishedAssignments = await assignedSubjectRepository.GetListAsync(a =>
            a.StandardId == timetable.StandardId &&
            a.Status == SubjectStatus.Published);
        var publishedWeeklySubjects = GetPublishedWeeklyLectureSubjects(weeklySlot, publishedAssignments);
        if (publishedWeeklySubjects.Count == 0)
        {
            throw new BusinessException(TimetableErrorCodes.WeeklyLectureSlotNotFound)
                .WithData("StandardId", timetable.StandardId);
        }

        var periodSlots = timetable.Slots
            .Where(s => s.SlotType == SlotType.Period)
            .ToList();
        if (periodSlots.Count == 0)
        {
            throw new BusinessException("Academic:Timetable:NoTeachingSlots")
                .WithData("StandardId", timetable.StandardId)
                .WithData("DivisionId", timetable.DivisionId);
        }

        EnsureNoDuplicateTimetableSlots(periodSlots);
        EnsureNoDuplicateTeacherOrRoomInsideSlot(periodSlots);
        await EnsureNoTeacherOrRoomConflictsAsync(timetable, periodSlots);
        EnsureWeeklyLectureCountsMatch(timetable, periodSlots, publishedWeeklySubjects);
    }

    private void ReplaceSlotsFromInput(Timetable timetable, SaveTimetableDto input)
    {
        var existingSlotIdsByPosition = timetable.Slots
            .GroupBy(slot => (slot.Day, slot.Sequence))
            .ToDictionary(group => group.Key, group => group.First().Id);

        timetable.ClearSlots();

        foreach (var (day, slots) in input.Timetable)
        {
            foreach (var slotDto in slots)
            {
                var slotId = existingSlotIdsByPosition.TryGetValue((day, slotDto.Sequence), out var existingSlotId)
                    ? existingSlotId
                    : GuidGenerator.Create();

                var slot = new TimetableSlot(
                    slotId,
                    timetable.Id,
                    day,
                    slotDto.Sequence,
                    slotDto.StartTime,
                    slotDto.EndTime,
                    slotDto.Type,
                    slotDto.BreakType,
                    slotDto.SubjectType,
                    slotDto.ElectiveGroupId);

                foreach (var subjectDto in slotDto.Subjects)
                {
                    slot.AddSubject(new TimetableSlotSubject(
                        GuidGenerator.Create(),
                        slot.Id,
                        subjectDto.SubjectId,
                        subjectDto.TeacherId,
                        subjectDto.RoomId));
                }

                timetable.AddSlot(slot);
            }
        }
    }

    private async Task EnsureNoTeacherOrRoomConflictsAsync(Timetable timetable)
    {
        var periodSlots = timetable.Slots
            .Where(s => s.SlotType == SlotType.Period)
            .ToList();

        EnsureNoDuplicateTimetableSlots(periodSlots);
        EnsureNoDuplicateTeacherOrRoomInsideSlot(periodSlots);
        await EnsureNoTeacherOrRoomConflictsAsync(timetable, periodSlots);
    }

    private static void EnsureNoDuplicateTimetableSlots(List<TimetableSlot> periodSlots)
    {
        var duplicateSlot = periodSlots
            .GroupBy(s => new { s.Day, s.StartTime, s.EndTime })
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateSlot != null)
        {
            throw new BusinessException(TimetableErrorCodes.InvalidSlotConfiguration)
                .WithData("Day", duplicateSlot.Key.Day)
                .WithData("StartTime", duplicateSlot.Key.StartTime)
                .WithData("EndTime", duplicateSlot.Key.EndTime)
                .WithData("message", "Duplicate teaching slot found in the timetable.");
        }
    }

    private static void EnsureNoDuplicateTeacherOrRoomInsideSlot(List<TimetableSlot> periodSlots)
    {
        foreach (var slot in periodSlots)
        {
            var teacherIds = slot.Subjects
                .Where(s => s.TeacherId.HasValue)
                .Select(s => s.TeacherId!.Value)
                .ToList();
            var duplicateTeacher = teacherIds.GroupBy(id => id).FirstOrDefault(g => g.Count() > 1);
            if (duplicateTeacher != null)
            {
                throw new BusinessException(TimetableErrorCodes.TeacherConflict)
                    .WithData("TeacherId", duplicateTeacher.Key)
                    .WithData("Day", slot.Day)
                    .WithData("StartTime", slot.StartTime)
                    .WithData("EndTime", slot.EndTime);
            }

            var roomIds = slot.Subjects
                .Where(s => s.RoomId.HasValue)
                .Select(s => s.RoomId!.Value)
                .ToList();
            var duplicateRoom = roomIds.GroupBy(id => id).FirstOrDefault(g => g.Count() > 1);
            if (duplicateRoom != null)
            {
                throw new BusinessException(TimetableErrorCodes.RoomConflict)
                    .WithData("RoomId", duplicateRoom.Key)
                    .WithData("Day", slot.Day)
                    .WithData("StartTime", slot.StartTime)
                    .WithData("EndTime", slot.EndTime);
            }
        }
    }

    private async Task EnsureNoTeacherOrRoomConflictsAsync(Timetable timetable, List<TimetableSlot> periodSlots)
    {
        foreach (var slot in periodSlots)
        {
            var teacherIds = slot.Subjects
                .Where(s => s.TeacherId.HasValue)
                .Select(s => s.TeacherId!.Value)
                .Distinct()
                .ToList();
            if (teacherIds.Count > 0)
            {
                await timetableManager.CheckTeacherConflictAsync(
                    timetable.StandardId,
                    timetable.DivisionId,
                    slot.Day,
                    slot.StartTime,
                    slot.EndTime,
                    teacherIds);
            }

            var roomIds = slot.Subjects
                .Where(s => s.RoomId.HasValue)
                .Select(s => s.RoomId!.Value)
                .Distinct()
                .ToList();
            if (roomIds.Count > 0)
            {
                await timetableManager.CheckRoomConflictAsync(
                    timetable.StandardId,
                    timetable.DivisionId,
                    slot.Day,
                    slot.StartTime,
                    slot.EndTime,
                    roomIds);
            }
        }
    }

    private static void EnsureWeeklyLectureCountsMatch(
        Timetable timetable,
        List<TimetableSlot> periodSlots,
        IReadOnlyCollection<WeeklyLectureSlotSubject> publishedWeeklySubjects)
    {
        var coreOccupied = periodSlots
            .Where(s => !s.ElectiveGroupId.HasValue)
            .SelectMany(s => s.Subjects)
            .GroupBy(s => s.SubjectId)
            .ToDictionary(g => g.Key, g => g.Count());

        var electiveOccupied = periodSlots
            .Where(s => s.ElectiveGroupId.HasValue)
            .GroupBy(s => s.ElectiveGroupId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var requirement in publishedWeeklySubjects)
        {
            var assigned = 0;
            Guid? referenceId = null;
            if (requirement.ElectiveGroupId.HasValue)
            {
                referenceId = requirement.ElectiveGroupId.Value;
                assigned = electiveOccupied.GetValueOrDefault(requirement.ElectiveGroupId.Value, 0);
            }
            else if (requirement.SubjectId.HasValue)
            {
                referenceId = requirement.SubjectId.Value;
                assigned = coreOccupied.GetValueOrDefault(requirement.SubjectId.Value, 0);
            }

            if (assigned != requirement.WeeklyLectures)
            {
                throw new BusinessException("Academic:Timetable:WeeklyLectureMismatch")
                    .WithData("StandardId", timetable.StandardId)
                    .WithData("DivisionId", timetable.DivisionId)
                    .WithData("SubjectOrElectiveGroupId", referenceId)
                    .WithData("RequiredWeeklyLectures", requirement.WeeklyLectures)
                    .WithData("AssignedWeeklyLectures", assigned);
            }
        }

        var allowedCoreSubjectIds = publishedWeeklySubjects
            .Where(s => s.SubjectId.HasValue)
            .Select(s => s.SubjectId!.Value)
            .ToHashSet();
        var allowedElectiveGroupIds = publishedWeeklySubjects
            .Where(s => s.ElectiveGroupId.HasValue)
            .Select(s => s.ElectiveGroupId!.Value)
            .ToHashSet();

        var unexpectedCore = coreOccupied.Keys.FirstOrDefault(subjectId => !allowedCoreSubjectIds.Contains(subjectId));
        if (unexpectedCore != Guid.Empty)
        {
            throw new BusinessException("Academic:Timetable:SubjectNotInWeeklyPlan")
                .WithData("SubjectId", unexpectedCore);
        }

        var unexpectedElective = electiveOccupied.Keys.FirstOrDefault(groupId => !allowedElectiveGroupIds.Contains(groupId));
        if (unexpectedElective != Guid.Empty)
        {
            throw new BusinessException("Academic:Timetable:ElectiveGroupNotInWeeklyPlan")
                .WithData("ElectiveGroupId", unexpectedElective);
        }
    }

    private static List<WeeklyLectureSlotSubject> GetPublishedWeeklyLectureSubjects(
        WeeklyLectureSlot? weeklySlot,
        IReadOnlyCollection<AssignedSubject> publishedAssignments)
    {
        if (weeklySlot == null || publishedAssignments.Count == 0)
        {
            return [];
        }

        var publishedSubjectIds = publishedAssignments
            .Where(assignment => !assignment.ElectiveGroupId.HasValue)
            .Select(assignment => assignment.SubjectId)
            .ToHashSet();
        var publishedElectiveGroupIds = publishedAssignments
            .Where(assignment => assignment.ElectiveGroupId.HasValue)
            .Select(assignment => assignment.ElectiveGroupId!.Value)
            .ToHashSet();

        return weeklySlot.Subjects
            .Where(subject =>
                (subject.SubjectId.HasValue && publishedSubjectIds.Contains(subject.SubjectId.Value)) ||
                (subject.ElectiveGroupId.HasValue && publishedElectiveGroupIds.Contains(subject.ElectiveGroupId.Value)))
            .ToList();
    }

    private async Task<Dictionary<Guid, List<Guid>>> GetSelectedElectiveSubjectIdsByGroupAsync(
        Guid standardId,
        Guid divisionId)
    {
        var students = await studentRepository.GetListAsync(student =>
            student.StandardId == standardId &&
            student.DivisionId == divisionId);
        var studentIds = students.Select(student => student.Id).ToList();
        if (studentIds.Count == 0)
        {
            return [];
        }

        var selections = await studentElectiveSelectionRepository.GetListAsync(selection =>
            studentIds.Contains(selection.StudentId));

        return selections
            .GroupBy(selection => selection.ElectiveGroupId)
            .Where(group => group.Select(selection => selection.StudentId).Distinct().Count() == studentIds.Count)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(selection => selection.SelectedSubjectId)
                    .Distinct()
                    .OrderBy(subjectId => subjectId)
                    .ToList());
    }

    private static List<TimetableSubjectResponseDto> BuildSlotSubjectResponses(
        TimetableSlot slot,
        IReadOnlyCollection<Subject> subjects,
        IReadOnlyCollection<Room> rooms,
        IReadOnlyCollection<Employee> employees,
        IReadOnlyDictionary<Guid, Guid> assignedSubjectBySubjectId,
        IReadOnlyDictionary<Guid, int> totalSlotsBySubject,
        IReadOnlyDictionary<Guid, int> totalSlotsByElective,
        IReadOnlyDictionary<Guid, int> occupiedBySubject,
        IReadOnlyDictionary<Guid, int> occupiedByElective,
        IReadOnlyDictionary<Guid, List<Guid>> selectedElectiveSubjectIdsByGroup)
    {
        return ResolveSlotSubjectsForDivision(slot, selectedElectiveSubjectIdsByGroup)
            .Select(sub =>
            {
                var subject = subjects.FirstOrDefault(s => s.Id == sub.SubjectId);
                var room = sub.RoomId.HasValue
                    ? rooms.FirstOrDefault(r => r.Id == sub.RoomId.Value)
                    : null;
                var totalSlots = totalSlotsBySubject.GetValueOrDefault(sub.SubjectId, 0);
                var occupiedSlots = occupiedBySubject.GetValueOrDefault(sub.SubjectId, 0);
                if (slot.ElectiveGroupId.HasValue)
                {
                    totalSlots = totalSlotsByElective.GetValueOrDefault(slot.ElectiveGroupId.Value, totalSlots);
                    occupiedSlots = occupiedByElective.GetValueOrDefault(slot.ElectiveGroupId.Value, occupiedSlots);
                }

                return new TimetableSubjectResponseDto
                {
                    SubjectAssignmentId = assignedSubjectBySubjectId.GetValueOrDefault(sub.SubjectId),
                    SubjectId = sub.SubjectId,
                    Name = subject?.Name ?? string.Empty,
                    Color = subject?.Color,
                    TeacherId = sub.TeacherId,
                    TeacherName = sub.TeacherId.HasValue
                        ? employees.FirstOrDefault(e => e.Id == sub.TeacherId.Value)?.EmployeeName
                        : null,
                    RoomId = sub.RoomId,
                    RoomName = room?.RoomName,
                    TotalSlots = totalSlots,
                    OccupiedSlots = occupiedSlots,
                    UnoccupiedSlots = totalSlots - occupiedSlots
                };
            }).ToList();
    }

    private static List<TimetableSlotSubject> ResolveSlotSubjectsForDivision(
        TimetableSlot slot,
        IReadOnlyDictionary<Guid, List<Guid>> selectedElectiveSubjectIdsByGroup)
    {
        var savedSubjects = slot.Subjects.ToList();
        if (!slot.ElectiveGroupId.HasValue ||
            !selectedElectiveSubjectIdsByGroup.TryGetValue(slot.ElectiveGroupId.Value, out var selectedSubjectIds) ||
            selectedSubjectIds.Count == 0)
        {
            return savedSubjects;
        }

        var fallback = savedSubjects.FirstOrDefault();
        return selectedSubjectIds
            .Select(subjectId =>
                savedSubjects.FirstOrDefault(subject => subject.SubjectId == subjectId) ??
                new TimetableSlotSubject(
                    Guid.Empty,
                    slot.Id,
                    subjectId,
                    fallback?.TeacherId,
                    fallback?.RoomId))
            .ToList();
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<byte[]> GetClasswiseTimetablePdfAsync(Guid standardId, Guid divisionId)
    {
        await EnsureCurrentUserCanAccessAsync(standardId, divisionId, "timetable-export");
        var standard = await standardRepository.GetAsync(standardId);
        var division = await divisionRepository.GetAsync(divisionId);

        // Load tenant extra properties for header
        Tenant? tenant = null;
        if (CurrentTenant.Id.HasValue)
        {
            using (CurrentTenant.Change(null)) // switch to host to read tenant
            {
                tenant = await tenantRepository.FindAsync(CurrentTenant.Id.Value);
            }
        }

        var instituteName = tenant?.GetProperty<string>("InstituteName")
            ?? CurrentTenant.Name
            ?? "School";
        var addressParts = new[]
        {
            tenant?.GetProperty<string>("Address1"),
            tenant?.GetProperty<string>("Address2"),
            tenant?.GetProperty<string>("Address3"),
            tenant?.GetProperty<string>("City"),
            tenant?.GetProperty<string>("State")
        }.Where(p => !string.IsNullOrWhiteSpace(p));
        var address = string.Join(", ", addressParts);
        var contactNumber = tenant?.GetProperty<string>("ContactNumber");
        var logoBytes = tenant?.GetProperty<byte[]>("Logo");

        // Fetch timetable data
        var timetableDto = await GetByStandardAndDivisionAsync(standardId, divisionId);

        // Ordered days
        var dayOrder = new[] { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY" };
        var availableDays = dayOrder.Where(d => timetableDto.Days.ContainsKey(d)).ToList();

        // Collect all unique slot rows (by sequence, start, end across all days)
        var allSlots = availableDays
            .SelectMany(day => timetableDto.Days[day].Select(slot => (day, slot)))
            .GroupBy(x => (x.slot.Sequence, x.slot.StartTime, x.slot.EndTime))
            .OrderBy(g => g.Key.Sequence)
            .Select(g =>
            {
                var anySlot = g.First().slot;
                var cells = new Dictionary<string, TimetablePdfCell>();
                foreach (var (day, slot) in g)
                {
                    cells[day] = new TimetablePdfCell
                    {
                        SubjectNames = slot.Subjects.Select(s => s.Name).ToList(),
                        TeacherNames = slot.Subjects.Select(s => s.TeacherName ?? string.Empty).ToList()
                    };
                }
                return new TimetablePdfSlotRow
                {
                    Sequence = anySlot.Sequence,
                    StartTime = anySlot.StartTime,
                    EndTime = anySlot.EndTime,
                    IsBreak = anySlot.Type == SlotType.Break,
                    Cells = cells
                };
            }).ToList();

        var pdfModel = new TimetableReportPdfModel
        {
            InstituteName = instituteName,
            Address = string.IsNullOrWhiteSpace(address) ? null : address,
            ContactNumber = contactNumber,
            LogoBytes = logoBytes,
            StandardName = standard.Name,
            DivisionName = division.Name,
            GeneratedAt = DateTime.Now,
            Days = availableDays,
            SlotRows = allSlots
        };

        return timetableReportPdfService.Generate(pdfModel);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<SlotConfigResponseDto> GetSlotConfigByStandardAsync(Guid standardId)
    {
        await EnsureCurrentUserCanAccessStandardAsync(standardId, "timetable-slot-config-read");
        var config = await lectureSlotConfigRepository.FindByStandardIdAsync(standardId);
        if (config is null)
        {
            return new SlotConfigResponseDto { StandardId = standardId };
        }

        var dayNames = new[] { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY" };

        return new SlotConfigResponseDto
        {
            StandardId = standardId,
            Items = config.SlotGroups.Select(group => new SlotConfigItemDto
            {
                ApplicableDays = Enumerable.Range(group.WeekRangeStart, group.WeekRangeEnd - group.WeekRangeStart + 1)
                    .Where(i => i < dayNames.Length)
                    .Select(i => dayNames[i])
                    .ToList(),
                TimeSlots = group.TimeSlots.Select((ts, index) => new TimeSlotConfigDto
                {
                    Sequence = index + 1,
                    StartTime = ts.StartTime.ToString("HH:mm"),
                    EndTime = ts.EndTime.ToString("HH:mm"),
                    Type = ts.SlotType,
                    BreakType = ts.BreakType
                }).ToList()
            }).ToList()
        };
    }

    private async Task EnsureCurrentUserCanAccessAsync(
        Guid standardId,
        Guid divisionId,
        string operation)
    {
        await userScopeAccessService.EnsureCurrentUserCanAccessAsync(
            new Timetable(Guid.Empty, CurrentTenant.Id, standardId, divisionId),
            operation: operation);
    }

    private async Task EnsureCurrentUserCanAccessStandardAsync(Guid standardId, string operation)
    {
        var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
        if (scope.IsAdmin || scope.HasTenantWideScope())
        {
            return;
        }

        var divisions = await divisionRepository.GetListAsync(division => division.StandardId == standardId);
        var hasAccess = divisions.Any(division => UserScopeQueryExtensions.HasAccessToEntity(
            new Timetable(Guid.Empty, CurrentTenant.Id, standardId, division.Id), scope));

        if (!hasAccess)
        {
            throw new BusinessException(UserScopeErrorCodes.AccessDenied)
                .WithData("Operation", operation)
                .WithData("StandardId", standardId);
        }
    }
}
