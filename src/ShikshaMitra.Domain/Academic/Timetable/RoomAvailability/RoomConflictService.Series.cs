using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Timetable.Timetables;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomConflictService
{
    public async Task<RoomConflictSession> CreateSessionAsync(IReadOnlyCollection<RoomConflictWindow> windows)
    {
        if (windows.Count == 0) throw new ArgumentException("At least one conflict window is required.", nameof(windows));
        var firstDate = windows.Min(window => window.Date.Date);
        var lastDate = windows.Max(window => window.Date.Date);
        var firstTime = windows.Min(window => window.StartTime);
        var lastTime = windows.Max(window => window.EndTime);
        var roomIds = windows.Select(window => window.RoomId).Distinct().ToArray();
        var teacherIds = windows.Where(window => window.TeacherId.HasValue).Select(window => window.TeacherId!.Value).Distinct().ToArray();
        var requestedStandards = windows.Where(window => window.StandardId.HasValue).Select(window => window.StandardId!.Value).Distinct().ToArray();
        var rooms = await _roomRepository.GetListAsync(room => Enumerable.Contains(roomIds, room.Id));
        var roomLookup = rooms.ToDictionary(room => room.Id, room => new RoomConflictRoom(room.Id, room.RoomName));
        var calendars = await _calendarRepository.GetListAsync(calendar => calendar.Status == CalendarStatus.Published &&
            calendar.StartDate.Date <= lastDate && calendar.EndDate.Date >= firstDate);
        var calendarIds = calendars.Select(calendar => calendar.Id).ToArray();
        var finalCalendarDate = calendars.Count == 0 ? lastDate : calendars.Max(calendar => calendar.EndDate.Date);
        // Next-working-day suggestions may extend beyond the last requested occurrence.
        var days = calendars.Count == 0 ? [] : await _calendarDayRepository.GetListAsync(day =>
            Enumerable.Contains(calendarIds, day.AcademicCalendarId) && day.Date.Date >= firstDate && day.Date.Date <= finalCalendarDate &&
            day.Status == CalendarDayStatus.Active && (day.AppliesToAll || (day.StandardId.HasValue && Enumerable.Contains(requestedStandards, day.StandardId.Value))));
        var rangeStart = firstDate.Add(firstTime);
        var rangeEnd = lastDate.Add(lastTime);
        var maintenance = await _maintenanceRepository.GetListAsync(row => Enumerable.Contains(roomIds, row.RoomId) &&
            row.Status != MaintenanceStatus.Cancelled && row.StartDateTime < rangeEnd && row.EndDateTime > rangeStart);
        var reservations = await _reservationRepository.GetListAsync(row => row.Date >= firstDate && row.Date <= lastDate &&
            (Enumerable.Contains(roomIds, row.RoomId) || (row.TeacherId.HasValue && Enumerable.Contains(teacherIds, row.TeacherId.Value))));
        var occupations = new List<TimetableConflictCandidate>();
        foreach (var day in windows.Select(window => window.Date.DayOfWeek).Where(day => day != DayOfWeek.Sunday).Distinct())
            occupations.AddRange(await _timetableRepository.GetRoomOccupationsAsync(roomIds, GetTimetableDayOfWeek(day),
                TimeOnly.FromTimeSpan(firstTime), TimeOnly.FromTimeSpan(lastTime)));
        var divisionIds = occupations.Select(row => row.DivisionId).Distinct().ToArray();
        var divisions = windows.Any(window => window.Date.DayOfWeek != DayOfWeek.Sunday)
            ? await ReadRoomDivisionsAsync((await _divisionRepository.GetQueryableAsync()).Where(row => (row.RoomNumber != null && row.RoomNumber != "") || Enumerable.Contains(divisionIds, row.Id)))
            : [];
        var standardIds = divisions.Select(row => row.StandardId).Concat(occupations.Select(row => row.StandardId)).Distinct().ToArray();
        var standardLookup = await ReadRoomStandardsAsync(standardIds);
        var maintenanceLookup = maintenance.ToLookup(row => row.RoomId);
        var reservationsByDate = reservations.ToLookup(row => row.Date.Date);
        var daysByCalendar = calendars.ToDictionary(calendar => calendar.Id,
            calendar => days.Where(row => row.AcademicCalendarId == calendar.Id).ToLookup(row => row.Date.Date));
        var occupationsByDay = occupations.GroupBy(row => row.Day).ToDictionary(group => group.Key,
            group => group.ToLookup(row => row.RoomId!.Value));
        var emptyDays = Array.Empty<AcademicCalendarDay>().ToLookup(row => row.Date.Date);
        var emptyOccupations = Array.Empty<TimetableConflictCandidate>().ToLookup(row => row.RoomId!.Value);
        var contexts = windows.Select(window => window.Date.Date).Distinct().ToDictionary(date => date, date =>
        {
            var calendar = calendars.FirstOrDefault(row => date >= row.StartDate.Date && date <= row.EndDate.Date);
            var dateReservations = reservationsByDate[date].ToList();
            return new RoomConflictReadContext(calendar, calendar == null ? emptyDays : daysByCalendar[calendar.Id],
                maintenanceLookup, dateReservations.ToLookup(row => row.RoomId),
                date.DayOfWeek == DayOfWeek.Sunday ? emptyOccupations : occupationsByDay.GetValueOrDefault(GetTimetableDayOfWeek(date.DayOfWeek), emptyOccupations),
                roomLookup, divisions, standardLookup, dateReservations.Where(row => row.TeacherId.HasValue).ToLookup(row => row.TeacherId!.Value));
        });
        return new RoomConflictSession(windows, window => CheckConflictsAsync(window.RoomId, window.Date, window.StartTime, window.EndTime,
            window.CurrentReservationId, window.TeacherId, window.StandardId, window.DivisionId, contexts[window.Date.Date]));
    }
}
