using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public class TimetableManager(
    ITimetableRepository timetableRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<Subject, Guid> subjectRepository,
    IRepository<Room, Guid> roomRepository,
    IRepository<Employee, Guid> employeeRepository) : DomainService
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

        var teacherIdList = teacherIds.ToList();
        var conflicts = otherTimetables
            .SelectMany(timetable => timetable.Slots
                .Where(slot => slot.Day == day && TimesOverlap(slot.StartTime, slot.EndTime, startTime, endTime))
                .SelectMany(slot => slot.Subjects
                    .Where(subject => subject.TeacherId.HasValue && teacherIdList.Contains(subject.TeacherId.Value))
                    .Select(subject => new ConflictRow(timetable, slot, subject, subject.TeacherId!.Value))))
            .ToList();

        if (conflicts.Count > 0)
        {
            var details = await BuildConflictDetailsAsync(conflicts, "teacher");
            throw BuildConflictException(
                TimetableErrorCodes.TeacherConflict,
                details,
                "One or more selected teachers are already assigned to another class during this time slot.");
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

        var roomIdList = roomIds.ToList();
        var conflicts = otherTimetables
            .SelectMany(timetable => timetable.Slots
                .Where(slot => slot.Day == day && TimesOverlap(slot.StartTime, slot.EndTime, startTime, endTime))
                .SelectMany(slot => slot.Subjects
                    .Where(subject => subject.RoomId.HasValue && roomIdList.Contains(subject.RoomId.Value))
                    .Select(subject => new ConflictRow(timetable, slot, subject, subject.RoomId!.Value))))
            .ToList();

        if (conflicts.Count > 0)
        {
            var details = await BuildConflictDetailsAsync(conflicts, "room");
            throw BuildConflictException(
                TimetableErrorCodes.RoomConflict,
                details,
                "One or more selected rooms are already assigned to another class during this time slot.");
        }
    }

    private static bool TimesOverlap(TimeOnly existingStart, TimeOnly existingEnd, TimeOnly requestedStart, TimeOnly requestedEnd)
    {
        return existingStart < requestedEnd && requestedStart < existingEnd;
    }

    private async Task<List<TimetableConflictDetail>> BuildConflictDetailsAsync(
        List<ConflictRow> conflicts,
        string type)
    {
        var standardIds = conflicts.Select(c => c.Timetable.StandardId).Distinct().ToList();
        var divisionIds = conflicts.Select(c => c.Timetable.DivisionId).Distinct().ToList();
        var subjectIds = conflicts.Select(c => c.Subject.SubjectId).Distinct().ToList();
        var resourceIds = conflicts.Select(c => c.ResourceId).Distinct().ToList();

        var standards = await standardRepository.GetListAsync(s => standardIds.Contains(s.Id));
        var divisions = await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id));
        var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
        var rooms = type == "room" ? await roomRepository.GetListAsync(r => resourceIds.Contains(r.Id)) : [];
        var employees = type == "teacher" ? await employeeRepository.GetListAsync(e => resourceIds.Contains(e.Id)) : [];

        return conflicts.Select(conflict =>
        {
            var standardName = standards.FirstOrDefault(s => s.Id == conflict.Timetable.StandardId)?.Name ?? "another class";
            var divisionName = divisions.FirstOrDefault(d => d.Id == conflict.Timetable.DivisionId)?.Name ?? "another division";
            var subjectName = subjects.FirstOrDefault(s => s.Id == conflict.Subject.SubjectId)?.Name ?? "assigned subject";
            var resourceName = type == "room"
                ? rooms.FirstOrDefault(r => r.Id == conflict.ResourceId)?.RoomName ?? "selected room"
                : employees.FirstOrDefault(e => e.Id == conflict.ResourceId)?.EmployeeName ?? "selected teacher";

            return new TimetableConflictDetail(
                type,
                conflict.ResourceId,
                resourceName,
                conflict.Timetable.StandardId,
                standardName,
                conflict.Timetable.DivisionId,
                divisionName,
                conflict.Subject.SubjectId,
                subjectName,
                conflict.Slot.Day.ToString(),
                FormatTime(conflict.Slot.StartTime),
                FormatTime(conflict.Slot.EndTime));
        }).ToList();
    }

    private static BusinessException BuildConflictException(
        string errorCode,
        List<TimetableConflictDetail> details,
        string fallbackMessage)
    {
        var first = details[0];
        var resourceLabel = first.Type == "room" ? "Room" : "Teacher";
        var extra = details.Count > 1 ? $" (+{details.Count - 1} more)" : string.Empty;
        var message =
            $"{resourceLabel} conflict: {first.ResourceName} is already assigned to {first.ConflictingStandardName} Division {first.ConflictingDivisionName} for {first.ConflictingSubjectName} on {first.Day} {first.StartTime}-{first.EndTime}{extra}.";

        return new BusinessException(errorCode)
            .WithData("message", message)
            .WithData("ConflictDetails", JsonSerializer.Serialize(details))
            .WithData("Day", first.Day)
            .WithData("StartTime", first.StartTime)
            .WithData("EndTime", first.EndTime)
            .WithData("ConflictingDivisionId", first.ConflictingDivisionId)
            .WithData("FallbackMessage", fallbackMessage);
    }

    private static string FormatTime(TimeOnly time)
    {
        return time.ToString("HH:mm");
    }

    private sealed record ConflictRow(
        Timetable Timetable,
        TimetableSlot Slot,
        TimetableSlotSubject Subject,
        Guid ResourceId);

    private sealed record TimetableConflictDetail(
        string Type,
        Guid ResourceId,
        string ResourceName,
        Guid ConflictingStandardId,
        string ConflictingStandardName,
        Guid ConflictingDivisionId,
        string ConflictingDivisionName,
        Guid ConflictingSubjectId,
        string ConflictingSubjectName,
        string Day,
        string StartTime,
        string EndTime);
}
