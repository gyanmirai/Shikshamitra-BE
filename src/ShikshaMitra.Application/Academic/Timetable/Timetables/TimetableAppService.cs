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
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
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
    TimetableReportPdfService timetableReportPdfService,
    ITenantRepository tenantRepository
) : ApplicationService, ITimetableAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<TimetableResponseDto> GetByStandardAndDivisionAsync(Guid standardId, Guid divisionId)
    {
        var timetable = await timetableRepository.FindByStandardAndDivisionAsync(standardId, divisionId);
        var slotConfig = await lectureSlotConfigRepository.FindByStandardIdAsync(standardId);

        var savedSlotsByDay = timetable?.Slots
            .GroupBy(s => s.Day)
            .ToDictionary(g => g.Key, g => g.ToList())
            ?? new Dictionary<TimetableDayOfWeek, List<TimetableSlot>>();

        var hasSavedData = savedSlotsByDay.Any(kv => kv.Value.Count > 0);

        // Weekly lecture slot is needed for totalSlots in subject summary
        var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(standardId);

        var assignedSubjects = await assignedSubjectRepository.GetListAsync(a => a.StandardId == standardId);
        var assignedSubjectBySubjectId = assignedSubjects
            .GroupBy(a => a.SubjectId)
            .ToDictionary(g => g.Key, g => g.First().Id);
        var needsSubjectData = hasSavedData || assignedSubjects.Count > 0;

        var subjects = needsSubjectData ? await subjectRepository.GetListAsync() : [];
        var electiveGroups = needsSubjectData ? await electiveGroupRepository.GetListAsync() : [];
        var rooms = hasSavedData ? await roomRepository.GetListAsync() : [];

        // Compute occupied slots per subject across all days in this timetable
        var occupiedBySubject = hasSavedData
            ? timetable!.Slots.SelectMany(s => s.Subjects).GroupBy(ss => ss.SubjectId)
                .ToDictionary(g => g.Key, g => g.Count())
            : new Dictionary<Guid, int>();

        // For slot-level subject TotalSlots/OccupiedSlots display (existing per-slot logic)
        var totalSlotsBySubject = weeklySlot?.Subjects
            .Where(s => s.SubjectId.HasValue)
            .ToDictionary(s => s.SubjectId!.Value, s => s.WeeklyLectures)
            ?? new Dictionary<Guid, int>();
        var totalSlotsByElective = weeklySlot?.Subjects
            .Where(s => s.ElectiveGroupId.HasValue)
            .ToDictionary(s => s.ElectiveGroupId!.Value, s => s.WeeklyLectures)
            ?? new Dictionary<Guid, int>();

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

            if (savedSlotsByDay.TryGetValue(day, out var savedSlots) && savedSlots.Count > 0)
            {
                response.Days[dayName] = savedSlots.OrderBy(s => s.Sequence).Select(slot =>
                {
                    var (resolvedType, resolvedBreakType) = slotTypeByDayAndTime.TryGetValue(
                        (slot.Day, slot.StartTime, slot.EndTime), out var configType)
                        ? configType
                        : (slot.SlotType, slot.BreakType);
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
                        ? electiveGroups.FirstOrDefault(eg => eg.Id == slot.ElectiveGroupId.Value)?.Name
                        : null,
                        ElectiveGroupColor = slot.ElectiveGroupId.HasValue
                        ? electiveGroups.FirstOrDefault(eg => eg.Id == slot.ElectiveGroupId.Value)?.Color
                        : null,
                        Subjects = slot.Subjects.Select(sub =>
                        {
                            var subject = subjects.FirstOrDefault(s => s.Id == sub.SubjectId);
                            var room = sub.RoomId.HasValue
                                ? rooms.FirstOrDefault(r => r.Id == sub.RoomId.Value) : null;
                            var totalSlots = totalSlotsBySubject.GetValueOrDefault(sub.SubjectId, 0);
                            if (totalSlots == 0 && slot.ElectiveGroupId.HasValue)
                                totalSlots = totalSlotsByElective.GetValueOrDefault(slot.ElectiveGroupId.Value, 0);
                            var occupiedSlots = occupiedBySubject.GetValueOrDefault(sub.SubjectId, 0);
                            return new TimetableSubjectResponseDto
                            {
                                SubjectAssignmentId = assignedSubjectBySubjectId.GetValueOrDefault(sub.SubjectId),
                                SubjectId = sub.SubjectId,
                                Name = subject?.Name ?? string.Empty,
                                Color = subject?.Color,
                                TeacherId = sub.TeacherId,
                                TeacherName = null,
                                RoomId = sub.RoomId,
                                RoomName = room?.RoomName,
                                TotalSlots = totalSlots,
                                OccupiedSlots = occupiedSlots,
                                UnoccupiedSlots = totalSlots - occupiedSlots
                            };
                        }).ToList()
                    };
                }).ToList();
            }
            else
            {
                // No saved data for this day — build placeholder from slot config
                var dayIndex = (int) day;
                var slotGroup = slotConfig?.SlotGroups
                    .FirstOrDefault(g => g.WeekRangeStart <= dayIndex && dayIndex <= g.WeekRangeEnd);

                response.Days[dayName] = slotGroup?.TimeSlots
                    .OrderBy(ts => ts.StartTime)
                    .Select((ts, index) => new TimetableSlotResponseDto
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
                    }).ToList() ?? [];
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
                    var weeklyEntry = weeklySlot?.Subjects.FirstOrDefault(w => w.SubjectId == assigned.SubjectId);
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

            // Elective subjects: one entry per distinct electiveGroupId
            var electiveEntries = assignedSubjects
                .Where(a => a.ElectiveGroupId.HasValue)
                .GroupBy(a => a.ElectiveGroupId!.Value)
                .Select(g =>
                {
                    var groupId = g.Key;
                    var group = electiveGroups.FirstOrDefault(eg => eg.Id == groupId);
                    var weeklyEntry = weeklySlot?.Subjects.FirstOrDefault(w => w.ElectiveGroupId == groupId);
                    var total = weeklyEntry?.WeeklyLectures ?? 0;
                    var occupied = electiveOccupied.GetValueOrDefault(groupId, 0);
                    var groupSubjects = g.Select(a =>
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
                    return new TimetableSubjectSummaryDto
                    {
                        Id = weeklyEntry?.Id ?? groupId,
                        SubjectAssignmentId = g.First().Id,
                        SubjectType = SubjectType.Elective,
                        ElectiveGroupId = groupId,
                        ElectiveGroupName = group?.Name,
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

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public async Task SaveAsync(Guid standardId, Guid divisionId, SaveTimetableDto input)
    {
        var timetable = await timetableRepository.FindByStandardAndDivisionAsync(standardId, divisionId);

        if (timetable is null)
        {
            timetable = new Timetable(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                standardId,
                divisionId);
        }

        timetable.ClearSlots();

        foreach (var (day, slots) in input.Timetable)
        {
            foreach (var slotDto in slots)
            {
                var slot = new TimetableSlot(
                    GuidGenerator.Create(),
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

        if (input.Status == TimetableStatus.Published)
        {
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

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<TimetableManagementResponseDto> GetManagementByStandardAsync(
        Guid standardId, GetTimetableManagementInput input)
    {
        var lectureSlotConfig = await lectureSlotConfigRepository.FindByStandardIdAsync(standardId);
        if (lectureSlotConfig is null)
        {
            throw new BusinessException(TimetableErrorCodes.LectureSlotNotCreated)
                .WithData("message", "Please first create lecture slot for selected standard");
        }

        var standard = await standardRepository.GetAsync(standardId);
        var divisionQueryable = await divisionRepository.GetQueryableAsync();
        var divisions = await AsyncExecuter.ToListAsync(
            divisionQueryable.Where(d => d.StandardId == standardId));

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

        var items = pagedDivisions.Select(division =>
        {
            var tt = timetables.FirstOrDefault(t => t.DivisionId == division.Id);
            return new DivisionTimetableStatusDto
            {
                DivisionId = division.Id,
                DivisionName = division.Name,
                Status = tt?.Status ?? TimetableStatus.NotCreated
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
        var day = Enum.Parse<TimetableDayOfWeek>(input.Day, ignoreCase: true);
        var startTime = input.StartTime;
        var endTime = input.EndTime;

        var teacherIds = input.Subjects.Select(s => s.TeacherId).ToList();
        await timetableManager.CheckTeacherConflictAsync(
            input.StandardId, input.DivisionId, day, startTime, endTime, teacherIds);

        var timetable = await timetableRepository.FindByStandardAndDivisionAsync(
            input.StandardId, input.DivisionId)
            ?? throw new BusinessException(TimetableErrorCodes.TimetableNotFound);

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
        var day = Enum.Parse<TimetableDayOfWeek>(input.Day, ignoreCase: true);
        var startTime = input.StartTime;
        var endTime = input.EndTime;

        var roomIds = input.Subjects.Select(s => s.RoomId).ToList();
        await timetableManager.CheckRoomConflictAsync(
            input.StandardId, input.DivisionId, day, startTime, endTime, roomIds);

        var timetable = await timetableRepository.FindByStandardAndDivisionAsync(
            input.StandardId, input.DivisionId)
            ?? throw new BusinessException(TimetableErrorCodes.TimetableNotFound);

        var slot = timetable.FindSlot(day, startTime, endTime)
            ?? throw new BusinessException(TimetableErrorCodes.TimetableSlotNotFound);

        foreach (var assignment in input.Subjects)
        {
            var subject = slot.Subjects.FirstOrDefault(s => s.SubjectId == assignment.SubjectId);
            subject?.AssignRoom(assignment.RoomId);
        }

        await timetableRepository.UpdateAsync(timetable);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<byte[]> GetClasswiseTimetablePdfAsync(Guid standardId, Guid divisionId)
    {
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
}
