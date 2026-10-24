using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.Portal;

/// <summary>Owner/guardian reads use persisted student associations, never staff wildcard scope.</summary>
[Authorize]
[RemoteService(false)]
public class PortalTimetableAppService(
    StudentPortalAccessService portalAccess,
    PortalStudentContextReader contextReader,
    ITimetableRepository timetables,
    ILectureSlotConfigRepository configs,
    IRepository<Subject, Guid> subjects,
    IRepository<StudentElectiveSelection, Guid> electives,
    IRepository<Employee, Guid> teachers,
    IRepository<Room, Guid> rooms,
    IRepository<AcademicCalendar, Guid> calendars,
    IRepository<AcademicCalendarDay, Guid> calendarDays,
    IRepository<SubstitutionRequest, Guid> substitutions)
    : ApplicationService, IPortalTimetableAppService
{
    private static readonly TimeZoneInfo SchoolZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public async Task<PortalTimetableContextDto> GetContextAsync(Guid? studentId = null, Guid? academicYearId = null)
    {
        var student = await ResolveStudentAsync(studentId);
        var result = (await contextReader.ReadAsync([student.Id], academicYearId))[student.Id];
        if (result.Error != null) throw result.Error;
        return result.Context!;
    }

    public async Task<PortalTimetableScheduleDto> GetScheduleAsync(PortalTimetableScheduleInput input)
    {
        // Validate again here for callers that bypass HTTP model binding.
        if (input.AcademicYearId == Guid.Empty || input.FromDate == default || input.ToDate == DateOnly.MaxValue || input.ToDate < input.FromDate ||
            input.ToDate.DayNumber - input.FromDate.DayNumber > 6)
            throw Invalid("Supply an academic year and a date range of at most seven days.");
        var context = await GetContextAsync(input.StudentId, input.AcademicYearId);
        var year = context.CurrentAcademicYear;
        if (year != null && (input.FromDate < year.StartDate || input.ToDate > year.EndDate))
            throw Invalid("The dates must be inside the enrolled academic year.");
        var result = new PortalTimetableScheduleDto { StudentId = context.Student.Id,
            AcademicYearId = input.AcademicYearId, FromDate = input.FromDate, ToDate = input.ToDate,
            Availability = context.Availability };
        for (var date = input.FromDate; date <= input.ToDate; date = date.AddDays(1))
            result.Days.Add(new() { Date = date, IsWorkingDay = context.WorkingDays.Contains(date.DayOfWeek.ToString().ToLowerInvariant()) });
        if (context.Availability != "available") return result;
        var standardId = context.Student.StandardId!.Value;
        var divisionId = context.Student.DivisionId!.Value;
        var timetable = await timetables.FindByStandardAndDivisionAsync(standardId, divisionId);
        if (timetable?.Status != TimetableStatus.Published) { result.Availability = "noPublishedTimetable"; return result; }
        var config = await configs.FindByStandardIdAsync(standardId);
        var selections = await electives.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == context.Student.Id);
        var selected = selections.GroupBy(x => x.ElectiveGroupId).ToDictionary(x => x.Key, x => x.Select(s => s.SelectedSubjectId).Distinct().ToList());
        if (selected.Values.Any(x => x.Count != 1)) throw Invalid("The student's elective selections are ambiguous.");
        if (timetable.Slots.Any(x => x.ElectiveGroupId.HasValue && !selected.ContainsKey(x.ElectiveGroupId.Value)))
            throw Invalid("The student's required elective selection is missing.");
        var subjectIds = timetable.Slots.SelectMany(x => x.Subjects).Select(x => x.SubjectId).Distinct().ToArray();
        var subjectMap = (await subjects.GetListAsync(x => Enumerable.Contains(subjectIds, x.Id))).ToDictionary(x => x.Id);
        var teacherIds = timetable.Slots.SelectMany(x => x.Subjects).Where(x => x.TeacherId.HasValue).Select(x => x.TeacherId!.Value).Distinct().ToArray();
        var teacherMap = (await teachers.GetListAsync(x => Enumerable.Contains(teacherIds, x.Id))).ToDictionary(x => x.Id);
        var roomIds = timetable.Slots.SelectMany(x => x.Subjects).Where(x => x.RoomId.HasValue).Select(x => x.RoomId!.Value).Distinct().ToArray();
        var roomMap = (await rooms.GetListAsync(x => Enumerable.Contains(roomIds, x.Id))).ToDictionary(x => x.Id);
        var student = await ResolveStudentAsync(context.Student.Id);
        var calendarIds = (await calendars.GetListAsync(x => x.Status == CalendarStatus.Published &&
            x.AcademicYearId == input.AcademicYearId && x.AcademicMediumId == student.AcademicMediumId)).Select(x => x.Id).ToArray();
        var start = input.FromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = input.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var days = await calendarDays.GetListAsync(x => Enumerable.Contains(calendarIds, x.AcademicCalendarId) &&
            x.Status == CalendarDayStatus.Active && x.Date >= start && x.Date < end &&
            (x.AppliesToAll || (x.StandardId == standardId && (!x.DivisionId.HasValue || x.DivisionId == divisionId))));
        var changes = await substitutions.GetListAsync(x => x.TimetableId == timetable.Id && x.AcademicYearId == input.AcademicYearId &&
            x.StandardId == standardId && x.DivisionId == divisionId && x.Date >= start && x.Date < end &&
            x.SubstituteTeacherId.HasValue && x.ConflictStatus != SubstitutionConflictStatus.HardBlock &&
            (x.Status == SubstitutionStatus.Acknowledged || x.Status == SubstitutionStatus.Completed ||
                (x.Status == SubstitutionStatus.Assigned && !x.AcknowledgementRequired)));
        foreach (var day in result.Days)
        {
            var exceptions = days.Where(x => DateOnly.FromDateTime(x.Date) == day.Date).ToList();
            var closure = exceptions.FirstOrDefault(x => x.DayType is DayType.Holiday or DayType.Vacation or DayType.EmergencyClosure);
            if (closure != null) { day.IsWorkingDay = false; day.DayType = "holiday"; day.DayTitle = closure.Title; continue; }
            if (!day.IsWorkingDay) { day.DayType = "nonWorkingDay"; continue; }
            day.DayTitle = exceptions.FirstOrDefault()?.Title;
            if (exceptions.Any(x => x.DayType == DayType.HalfDay)) day.DayType = "halfDay";
            else if (exceptions.Any(x => x.DayType == DayType.Event)) day.DayType = "event";
            var dayIndex = ((int)day.Date.DayOfWeek + 6) % 7;
            var slots = timetable.Slots.Where(x => (int)x.Day == dayIndex).OrderBy(x => x.StartTime).ThenBy(x => x.Sequence);
            foreach (var slot in slots)
            {
                if (slot.EndTime <= slot.StartTime) throw Invalid("A timetable period has invalid start/end times.");
                if (slot.SlotType == SlotType.Break)
                {
                    day.Periods.Add(Break(slot.Id, slot.StartTime, slot.EndTime, slot.BreakType, day.Date));
                    continue;
                }
                var options = slot.Subjects.Where(x => !slot.ElectiveGroupId.HasValue ||
                    (selected.TryGetValue(slot.ElectiveGroupId.Value, out var choice) && choice.Contains(x.SubjectId))).ToList();
                if (options.Count > 1) throw Invalid("The timetable has multiple subjects for one student period.");
                foreach (var item in options)
                {
                    if (!subjectMap.TryGetValue(item.SubjectId, out var subject)) throw Invalid("A scheduled subject is unavailable.");
                    var period = new PortalTimetablePeriodDto { PeriodId = item.Id, PeriodNumber = slot.Sequence,
                        Subject = new() { Id = subject.Id, Name = subject.Name, Color = subject.Color },
                        StartDateTime = At(day.Date, slot.StartTime), EndDateTime = At(day.Date, slot.EndTime) };
                    if (item.TeacherId.HasValue && teacherMap.TryGetValue(item.TeacherId.Value, out var teacher))
                        period.Teacher = new() { Id = teacher.Id, Name = teacher.EmployeeName };
                    if (item.RoomId.HasValue && roomMap.TryGetValue(item.RoomId.Value, out var room))
                        period.Room = new() { Id = room.Id, Name = room.RoomName };
                    var replacements = changes.Where(x => DateOnly.FromDateTime(x.Date) == day.Date &&
                        x.TimetableSlotId == slot.Id && x.TimetableSlotSubjectId == item.Id && x.SubjectId == item.SubjectId &&
                        x.StartTime == slot.StartTime && x.EndTime == slot.EndTime).ToList();
                    if (replacements.Count > 1) throw Invalid("The timetable has conflicting confirmed substitutions.");
                    if (replacements.Count == 1)
                    {
                        var change = replacements[0];
                        period.ReplacementTeacher = new() { Id = change.SubstituteTeacherId!.Value, Name = change.SubstituteTeacherName ?? string.Empty };
                        if (change.AssignedRoomId.HasValue) period.Room = new() { Id = change.AssignedRoomId.Value, Name = change.AssignedRoomName ?? string.Empty };
                        period.Status = "substituted";
                    }
                    day.Periods.Add(period);
                }
            }
            // Breaks belong to the class slot configuration even when saved lesson slots omit them.
            foreach (var group in config?.SlotGroups.Where(x => x.ActiveDays.Contains(dayIndex)) ?? [])
                foreach (var slot in group.TimeSlots.Where(x => x.SlotType == SlotType.Break))
                    if (!day.Periods.Any(x => x.StartDateTime == At(day.Date, slot.StartTime) && x.EndDateTime == At(day.Date, slot.EndTime)))
                        day.Periods.Add(Break(slot.Id, slot.StartTime, slot.EndTime, slot.BreakType, day.Date));
            day.Periods = day.Periods.OrderBy(x => x.StartDateTime).ThenBy(x => x.PeriodId).ToList();
            foreach (var halfDay in exceptions.Where(x => x.DayType == DayType.HalfDay))
            {
                if (!halfDay.StartTime.HasValue || !halfDay.EndTime.HasValue || halfDay.EndTime <= halfDay.StartTime)
                    throw Invalid("A published half day has invalid working hours.");
                day.Periods.RemoveAll(x => x.StartDateTime.TimeOfDay < halfDay.StartTime.Value || x.EndDateTime.TimeOfDay > halfDay.EndTime.Value);
            }
            foreach (var period in day.Periods)
                if (period.Status != "substituted") period.Status = SchoolNow() >= period.EndDateTime ? "completed" :
                    SchoolNow() >= period.StartDateTime ? "ongoing" : "scheduled";
        }
        return result;
    }

    private async Task<Student> ResolveStudentAsync(Guid? studentId)
    {
        return await portalAccess.ResolveStudentAsync(studentId);
    }

    private DateTimeOffset SchoolNow() => TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(Clock.Now, DateTimeKind.Utc)), SchoolZone);
    private static DateTimeOffset At(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new(local, SchoolZone.GetUtcOffset(local));
    }
    private static PortalTimetablePeriodDto Break(Guid id, TimeOnly start, TimeOnly end, BreakType? type, DateOnly date) => new()
    {
        PeriodId = id, PeriodType = "break", BreakType = type?.ToString().ToLowerInvariant(),
        Title = type == global::ShikshaMitra.Academic.Timetable.BreakType.Lunch ? "Lunch Break" : "Short Break",
        StartDateTime = At(date, start), EndDateTime = At(date, end)
    };
    private static BusinessException Invalid(string reason) => new BusinessException("Academic:PortalTimetable:InvalidContext").WithData("Reason", reason);
}
