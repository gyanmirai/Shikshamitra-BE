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
    /// <summary>Validates all submitted periods against one read of competing assignments.</summary>
    public async Task CheckConflictsAsync(
        Guid standardId,
        Guid divisionId,
        IReadOnlyCollection<TimetableSlot> slots)
    {
        if (slots.Count == 0)
        {
            return;
        }

        var teachers = slots.SelectMany(slot => slot.Subjects)
            .Where(subject => subject.TeacherId.HasValue)
            .Select(subject => subject.TeacherId!.Value).Distinct().ToArray();
        var rooms = slots.SelectMany(slot => slot.Subjects)
            .Where(subject => subject.RoomId.HasValue)
            .Select(subject => subject.RoomId!.Value).Distinct().ToArray();
        if (teachers.Length == 0 && rooms.Length == 0)
        {
            return;
        }

        var candidates = await timetableRepository.GetConflictCandidatesAsync(
            standardId, divisionId, slots.Select(slot => slot.Day).Distinct().ToArray(),
            slots.Min(slot => slot.StartTime), slots.Max(slot => slot.EndTime), teachers, rooms);
        var candidatesByDay = candidates.ToLookup(candidate => candidate.Day);

        // Preserve input slot order and teacher-before-room error precedence.
        foreach (var slot in slots)
        {
            await CheckResourceConflictsAsync(candidatesByDay[slot.Day], slot.StartTime, slot.EndTime,
                slot.Subjects.Where(subject => subject.TeacherId.HasValue)
                    .Select(subject => subject.TeacherId!.Value), "teacher");
            await CheckResourceConflictsAsync(candidatesByDay[slot.Day], slot.StartTime, slot.EndTime,
                slot.Subjects.Where(subject => subject.RoomId.HasValue)
                    .Select(subject => subject.RoomId!.Value), "room");
        }
    }

    public async Task CheckTeacherConflictAsync(
        Guid standardId,
        Guid divisionId,
        TimetableDayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime,
        IEnumerable<Guid> teacherIds)
    {
        var teachers = teacherIds.Distinct().ToArray();
        if (teachers.Length == 0) return;

        var candidates = await timetableRepository.GetConflictCandidatesAsync(
            standardId, divisionId, [day], startTime, endTime, teachers, []);
        await CheckResourceConflictsAsync(candidates, startTime, endTime, teachers, "teacher");
    }

    public async Task CheckRoomConflictAsync(
        Guid standardId,
        Guid divisionId,
        TimetableDayOfWeek day,
        TimeOnly startTime,
        TimeOnly endTime,
        IEnumerable<Guid> roomIds)
    {
        var rooms = roomIds.Distinct().ToArray();
        if (rooms.Length == 0) return;

        var candidates = await timetableRepository.GetConflictCandidatesAsync(
            standardId, divisionId, [day], startTime, endTime, [], rooms);
        await CheckResourceConflictsAsync(candidates, startTime, endTime, rooms, "room");
    }

    private async Task CheckResourceConflictsAsync(
        IEnumerable<TimetableConflictCandidate> candidates,
        TimeOnly startTime,
        TimeOnly endTime,
        IEnumerable<Guid> resourceIds,
        string type)
    {
        var ids = resourceIds.ToHashSet();
        if (ids.Count == 0) return;

        var conflicts = candidates
            .Where(candidate => TimesOverlap(candidate.StartTime, candidate.EndTime, startTime, endTime))
            .Select(candidate => new { Candidate = candidate, ResourceId = type == "teacher" ? candidate.TeacherId : candidate.RoomId })
            .Where(row => row.ResourceId.HasValue && ids.Contains(row.ResourceId.Value))
            .Select(row => new ConflictRow(row.Candidate, row.ResourceId!.Value))
            .ToList();
        if (conflicts.Count == 0) return;

        var details = await BuildConflictDetailsAsync(conflicts, type);
        throw BuildConflictException(
            type == "teacher" ? TimetableErrorCodes.TeacherConflict : TimetableErrorCodes.RoomConflict,
            details,
            type == "teacher"
                ? "One or more selected teachers are already assigned to another class during this time slot."
                : "One or more selected rooms are already assigned to another class during this time slot.");
    }

    private static bool TimesOverlap(TimeOnly existingStart, TimeOnly existingEnd, TimeOnly requestedStart, TimeOnly requestedEnd)
    {
        return existingStart < requestedEnd && requestedStart < existingEnd;
    }

    private async Task<List<TimetableConflictDetail>> BuildConflictDetailsAsync(
        List<ConflictRow> conflicts,
        string type)
    {
        var standardIds = conflicts.Select(c => c.Candidate.StandardId).Distinct().ToList();
        var divisionIds = conflicts.Select(c => c.Candidate.DivisionId).Distinct().ToList();
        var subjectIds = conflicts.Select(c => c.Candidate.SubjectId).Distinct().ToList();
        var resourceIds = conflicts.Select(c => c.ResourceId).Distinct().ToList();

        var standards = await standardRepository.GetListAsync(s => standardIds.Contains(s.Id));
        var divisions = await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id));
        var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));
        var rooms = type == "room" ? await roomRepository.GetListAsync(r => resourceIds.Contains(r.Id)) : [];
        var employees = type == "teacher" ? await employeeRepository.GetListAsync(e => resourceIds.Contains(e.Id)) : [];

        return conflicts.Select(conflict =>
        {
            var standardName = standards.FirstOrDefault(s => s.Id == conflict.Candidate.StandardId)?.Name ?? "another class";
            var divisionName = divisions.FirstOrDefault(d => d.Id == conflict.Candidate.DivisionId)?.Name ?? "another division";
            var subjectName = subjects.FirstOrDefault(s => s.Id == conflict.Candidate.SubjectId)?.Name ?? "assigned subject";
            var resourceName = type == "room"
                ? rooms.FirstOrDefault(r => r.Id == conflict.ResourceId)?.RoomName ?? "selected room"
                : employees.FirstOrDefault(e => e.Id == conflict.ResourceId)?.EmployeeName ?? "selected teacher";

            return new TimetableConflictDetail(
                type,
                conflict.ResourceId,
                resourceName,
                conflict.Candidate.StandardId,
                standardName,
                conflict.Candidate.DivisionId,
                divisionName,
                conflict.Candidate.SubjectId,
                subjectName,
                conflict.Candidate.Day.ToString(),
                FormatTime(conflict.Candidate.StartTime),
                FormatTime(conflict.Candidate.EndTime));
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
        TimetableConflictCandidate Candidate,
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
