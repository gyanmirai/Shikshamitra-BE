using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public class TimetableManager(ITimetableRepository timetableRepository) : DomainService
{
    public async Task CheckTeacherConflictAsync(
        Guid standardId,
        Guid divisionId,
        TimetableDayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime,
        IEnumerable<Guid> teacherIds)
    {
        var allTimetables = await timetableRepository.GetAllWithSlotsAsync();

        var otherTimetables = allTimetables
            .Where(t =>
                (t.StandardId != standardId || t.DivisionId != divisionId) &&
                t.Status != TimetableStatus.NotCreated)
            .ToList();

        foreach (var timetable in otherTimetables)
        {
            var conflictingSlots = timetable.Slots
                .Where(s => s.Day == day && s.StartTime == startTime && s.EndTime == endTime)
                .SelectMany(s => s.Subjects)
                .Where(s => s.TeacherId.HasValue && teacherIds.Contains(s.TeacherId.Value));

            if (conflictingSlots.Any())
            {
                throw new BusinessException(TimetableErrorCodes.TeacherConflict)
                    .WithData("ConflictingDivisionId", timetable.DivisionId)
                    .WithData("Day", day)
                    .WithData("StartTime", startTime)
                    .WithData("EndTime", endTime)
                    .WithData("message", "One or more selected teachers are already assigned to another class during this time slot.");
            }
        }
    }

    public async Task CheckRoomConflictAsync(
        Guid standardId,
        Guid divisionId,
        TimetableDayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime,
        IEnumerable<Guid> roomIds)
    {
        var allTimetables = await timetableRepository.GetAllWithSlotsAsync();

        var otherTimetables = allTimetables
            .Where(t =>
                (t.StandardId != standardId || t.DivisionId != divisionId) &&
                t.Status != TimetableStatus.NotCreated)
            .ToList();

        foreach (var timetable in otherTimetables)
        {
            var conflictingSlots = timetable.Slots
                .Where(s => s.Day == day && s.StartTime == startTime && s.EndTime == endTime)
                .SelectMany(s => s.Subjects)
                .Where(s => s.RoomId.HasValue && roomIds.Contains(s.RoomId.Value));

            if (conflictingSlots.Any())
            {
                throw new BusinessException(TimetableErrorCodes.RoomConflict)
                    .WithData("ConflictingDivisionId", timetable.DivisionId)
                    .WithData("Day", day)
                    .WithData("StartTime", startTime)
                    .WithData("EndTime", endTime)
                    .WithData("message", "One or more selected rooms are already assigned to another class during this time slot.");
            }
        }
    }
}
