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
using Volo.Abp.Domain.Repositories;

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
    IRepository<Room, Guid> roomRepository
) : ApplicationService, ITimetableAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<TimetableResponseDto> GetByStandardAndDivisionAsync(Guid standardId, Guid divisionId)
    {
        var timetable = await timetableRepository.FindByStandardAndDivisionAsync(standardId, divisionId);
        if (timetable is null)
        {
            return new TimetableResponseDto();
        }

        var subjects = await subjectRepository.GetListAsync();
        var electiveGroups = await electiveGroupRepository.GetListAsync();
        var rooms = await roomRepository.GetListAsync();

        // Compute occupied slots per subject across all days in this timetable
        var occupiedBySubject = timetable.Slots
            .SelectMany(s => s.Subjects)
            .GroupBy(ss => ss.SubjectId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Get weekly lecture allocations (total slots) per subject for this standard
        var weeklySlot = await weeklyLectureSlotRepository.FindByStandardIdAsync(timetable.StandardId);
        var totalSlotsBySubject = weeklySlot?.Subjects
            .Where(s => s.SubjectId.HasValue)
            .ToDictionary(s => s.SubjectId!.Value, s => s.WeeklyLectures)
            ?? new Dictionary<Guid, int>();
        var totalSlotsByElective = weeklySlot?.Subjects
            .Where(s => s.ElectiveGroupId.HasValue)
            .ToDictionary(s => s.ElectiveGroupId!.Value, s => s.WeeklyLectures)
            ?? new Dictionary<Guid, int>();

        var response = new TimetableResponseDto();

        var slotsByDay = timetable.Slots.GroupBy(s => s.Day);
        foreach (var dayGroup in slotsByDay)
        {
            var dayName = dayGroup.Key.ToString().ToUpperInvariant();
            response.Days[dayName] = dayGroup.OrderBy(s => s.Sequence).Select(slot => new TimetableSlotResponseDto
            {
                Sequence = slot.Sequence,
                StartTime = slot.StartTime.ToString("HH:mm"),
                EndTime = slot.EndTime.ToString("HH:mm"),
                Type = slot.SlotType,
                BreakType = slot.BreakType,
                SubjectType = slot.SubjectType,
                ElectiveGroupId = slot.ElectiveGroupId,
                ElectiveGroupName = slot.ElectiveGroupId.HasValue
                    ? electiveGroups.FirstOrDefault(eg => eg.Id == slot.ElectiveGroupId.Value)?.Name
                    : null,
                Subjects = slot.Subjects.Select(sub =>
                {
                    var subject = subjects.FirstOrDefault(s => s.Id == sub.SubjectId);
                    var room = sub.RoomId.HasValue
                        ? rooms.FirstOrDefault(r => r.Id == sub.RoomId.Value) : null;
                    return new TimetableSubjectResponseDto
                    {
                        Id = sub.SubjectId,
                        Name = subject?.Name ?? string.Empty,
                        Color = subject?.Color,
                        TeacherId = sub.TeacherId,
                        TeacherName = null,
                        RoomId = sub.RoomId,
                        RoomName = room?.RoomName
                    };
                }).ToList()
            }).ToList();
        }

        // Build per-subject weekly summary
        var allSubjectIds = timetable.Slots
            .SelectMany(s => s.Subjects)
            .Select(ss => ss.SubjectId)
            .Distinct()
            .ToList();

        response.SubjectSummary = allSubjectIds.Select(subjectId =>
        {
            var subject = subjects.FirstOrDefault(s => s.Id == subjectId);
            var totalSlots = totalSlotsBySubject.GetValueOrDefault(subjectId, 0);
            var slotForElective = timetable.Slots
                .FirstOrDefault(s => s.Subjects.Any(ss => ss.SubjectId == subjectId) && s.ElectiveGroupId.HasValue);
            if (totalSlots == 0 && slotForElective?.ElectiveGroupId.HasValue == true)
                totalSlots = totalSlotsByElective.GetValueOrDefault(slotForElective.ElectiveGroupId!.Value, 0);
            var occupied = occupiedBySubject.GetValueOrDefault(subjectId, 0);
            return new TimetableSubjectSummaryDto
            {
                Id = subjectId,
                Name = subject?.Name ?? string.Empty,
                Color = subject?.Color,
                TotalSlots = totalSlots,
                OccupiedSlots = occupied,
                UnoccupiedSlots = totalSlots - occupied
            };
        }).ToList();

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
