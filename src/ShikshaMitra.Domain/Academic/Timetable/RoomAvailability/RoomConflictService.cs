using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public partial class RoomConflictService : DomainService
{
    private readonly IRepository<RoomReservation, Guid> _reservationRepository;
    private readonly IRepository<RoomMaintenanceBlock, Guid> _maintenanceRepository;
    private readonly ITimetableRepository _timetableRepository;
    private readonly IRepository<AcademicCalendar, Guid> _calendarRepository;
    private readonly IRepository<AcademicCalendarDay, Guid> _calendarDayRepository;
    private readonly IRepository<Room, Guid> _roomRepository;
    private readonly IRepository<Division, Guid> _divisionRepository;
    private readonly IRepository<Standard, Guid> _standardRepository;

    public RoomConflictService(
        IRepository<RoomReservation, Guid> reservationRepository,
        IRepository<RoomMaintenanceBlock, Guid> maintenanceRepository,
        ITimetableRepository timetableRepository,
        IRepository<AcademicCalendar, Guid> calendarRepository,
        IRepository<AcademicCalendarDay, Guid> calendarDayRepository,
        IRepository<Room, Guid> roomRepository,
        IRepository<Division, Guid> divisionRepository,
        IRepository<Standard, Guid> standardRepository)
    {
        _reservationRepository = reservationRepository;
        _maintenanceRepository = maintenanceRepository;
        _timetableRepository = timetableRepository;
        _calendarRepository = calendarRepository;
        _calendarDayRepository = calendarDayRepository;
        _roomRepository = roomRepository;
        _divisionRepository = divisionRepository;
        _standardRepository = standardRepository;
    }

    public async Task<ConflictCheckResult> CheckConflictsAsync(
        Guid roomId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? currentReservationId = null,
        Guid? teacherId = null,
        Guid? standardId = null,
        Guid? divisionId = null)
    {
        return await CheckConflictsAsync(roomId, date, startTime, endTime, currentReservationId,
            teacherId, standardId, divisionId, null);
    }

    public Task<ConflictCheckResult> CheckScopedConflictsAsync(
        RoomConflictAcademicScope academicScope, Guid roomId, DateTime date, TimeSpan startTime, TimeSpan endTime,
        Guid? currentReservationId = null, Guid? teacherId = null) =>
        CheckConflictsAsync(roomId, date, startTime, endTime, currentReservationId,
            teacherId, academicScope.StandardId, null, null, academicScope);

    /// <summary>Checks a room list using one request-local set of availability data.</summary>
    public async Task<IReadOnlyDictionary<Guid, ConflictCheckResult>> CheckRoomsAsync(
        IReadOnlyCollection<Room> rooms,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? standardId = null,
        Guid? divisionId = null)
    {
        return await CheckRoomSnapshotsAsync(rooms.Select(room => new RoomConflictRoom(room.Id, room.RoomName)).ToList(),
            date, startTime, endTime, standardId, divisionId);
    }

    public async Task<IReadOnlyDictionary<Guid, ConflictCheckResult>> CheckRoomSnapshotsAsync(
        IReadOnlyCollection<RoomConflictRoom> rooms, DateTime date, TimeSpan startTime, TimeSpan endTime,
        Guid? standardId = null, Guid? divisionId = null)
    {
        if (rooms.Count == 0) return new Dictionary<Guid, ConflictCheckResult>();

        var context = await BuildReadContextAsync(rooms, date, startTime, endTime, standardId, divisionId);
        var results = new Dictionary<Guid, ConflictCheckResult>();
        foreach (var room in rooms)
        {
            results[room.Id] = await CheckConflictsAsync(room.Id, date, startTime, endTime,
                null, null, standardId, divisionId, context);
        }

        return results;
    }

    /// <summary>Checks a room's periods against one request-local set of availability data.</summary>
    public async Task<IReadOnlyList<ConflictCheckResult>> CheckRoomPeriodsAsync(
        Room room,
        DateTime date,
        IReadOnlyList<(TimeSpan Start, TimeSpan End)> periods)
    {
        if (periods.Count == 0) return [];

        var context = await BuildReadContextAsync([new RoomConflictRoom(room.Id, room.RoomName)], date,
            periods.Min(period => period.Start), periods.Max(period => period.End), null, null);
        var results = new List<ConflictCheckResult>(periods.Count);
        foreach (var period in periods)
        {
            results.Add(await CheckConflictsAsync(room.Id, date, period.Start, period.End,
                null, null, null, null, context));
        }

        return results;
    }

    private async Task<RoomConflictReadContext> BuildReadContextAsync(
        IReadOnlyCollection<RoomConflictRoom> rooms,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? standardId,
        Guid? divisionId)
    {
        var roomIds = rooms.Select(room => room.Id).ToList();
        var calendar = await _calendarRepository.FirstOrDefaultAsync(c =>
            c.Status == CalendarStatus.Published && date.Date >= c.StartDate.Date && date.Date <= c.EndDate.Date);
        var days = calendar == null
            ? []
            : await _calendarDayRepository.GetListAsync(d => d.AcademicCalendarId == calendar.Id &&
                d.Date.Date >= date.Date && d.Date.Date <= calendar.EndDate.Date && d.Status == CalendarDayStatus.Active &&
                (d.AppliesToAll || (standardId.HasValue && d.StandardId == standardId.Value)));
        var currentDay = days.FirstOrDefault(d => d.Date.Date == date.Date &&
            (d.AppliesToAll || (!d.DivisionId.HasValue || (divisionId.HasValue && d.DivisionId == divisionId.Value))));
        var calendarAllowed = currentDay == null || !CalendarDayBlockingRules.Blocks(currentDay.DayType, "timetable");
        var requestedStart = date.Date.Add(startTime);
        var requestedEnd = date.Date.Add(endTime);
        var maintenance = await _maintenanceRepository.GetListAsync(m => roomIds.Contains(m.RoomId) &&
            m.Status != MaintenanceStatus.Cancelled && m.StartDateTime < requestedEnd && m.EndDateTime > requestedStart);
        var reservations = await _reservationRepository.GetListAsync(r => roomIds.Contains(r.RoomId) && r.Date == date.Date);
        var checkClassSchedule = calendarAllowed && date.DayOfWeek != DayOfWeek.Sunday;
        var occupations = checkClassSchedule
            ? await _timetableRepository.GetRoomOccupationsAsync(roomIds,
                GetTimetableDayOfWeek(date.DayOfWeek), TimeOnly.FromTimeSpan(startTime), TimeOnly.FromTimeSpan(endTime))
            : [];
        var timetableDivisionIds = occupations.Select(occupation => occupation.DivisionId).Distinct().ToList();
        var divisions = checkClassSchedule
            ? await ReadRoomDivisionsAsync((await _divisionRepository.GetQueryableAsync()).Where(d => (d.RoomNumber != null && d.RoomNumber != string.Empty) ||
                timetableDivisionIds.Contains(d.Id)))
            : [];
        var standardIds = divisions.Select(division => division.StandardId)
            .Concat(occupations.Select(occupation => occupation.StandardId)).Distinct().ToList();
        var standards = await ReadRoomStandardsAsync(standardIds);
        return new RoomConflictReadContext(calendar, days.ToLookup(row => row.Date.Date), maintenance.ToLookup(row => row.RoomId),
            reservations.ToLookup(row => row.RoomId), occupations.ToLookup(row => row.RoomId!.Value), rooms.ToDictionary(room => room.Id),
            divisions, standards, Array.Empty<RoomReservation>().ToLookup(row => row.TeacherId.GetValueOrDefault()));
    }

    private async Task<ConflictCheckResult> CheckConflictsAsync(
        Guid roomId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? currentReservationId,
        Guid? teacherId,
        Guid? standardId,
        Guid? divisionId,
        RoomConflictReadContext? context,
        RoomConflictAcademicScope? academicScope = null)
    {
        var result = new ConflictCheckResult();
        var conflicts = new List<ConflictItem>();

        // 1. Check Academic Calendar
        var academicMediumId = academicScope?.AcademicMediumId;
        var academicYearId = academicScope?.AcademicYearId;
        var legacyCalendarYearMatches = academicYearId.HasValue && academicScope?.MediumAcademicYearId == academicYearId;
        var calendar = context != null ? context.Calendar : await _calendarRepository.FirstOrDefaultAsync(c =>
            (!academicMediumId.HasValue || c.AcademicMediumId == academicMediumId.Value) &&
            (!academicYearId.HasValue || c.AcademicYearId == academicYearId.Value ||
                c.AcademicYearId == null && legacyCalendarYearMatches) &&
            c.Status == CalendarStatus.Published &&
            date.Date >= c.StartDate.Date &&
            date.Date <= c.EndDate.Date);

        bool calAllowed = true;
        string? calMsg = null;
        DateTime? suggestedNextWorking = null;

        if (calendar != null)
        {
            var day = context != null
                ? context.Days[date.Date].FirstOrDefault(d =>
                    (d.AppliesToAll || (standardId.HasValue && d.StandardId == standardId.Value &&
                        (!d.DivisionId.HasValue || (divisionId.HasValue && d.DivisionId == divisionId.Value)))))
                : await _calendarDayRepository.FirstOrDefaultAsync(d =>
                d.AcademicCalendarId == calendar.Id &&
                d.Date.Date == date.Date &&
                d.Status == CalendarDayStatus.Active &&
                (d.AppliesToAll || 
                 (standardId.HasValue && d.StandardId == standardId.Value && 
                  (!d.DivisionId.HasValue || (divisionId.HasValue && d.DivisionId == divisionId.Value)))));

            if (day != null)
            {
                bool isBlocked = CalendarDayBlockingRules.Blocks(day.DayType, "timetable");
                if (isBlocked)
                {
                    calAllowed = false;
                    calMsg = $"Date blocked: {date:MMM dd} falls on {day.Title} ({day.DayType}).";

                    // Suggest next working day
                    var nextWorking = date.AddDays(1);
                    while (nextWorking <= calendar.EndDate)
                    {
                        var nextDay = context != null
                            ? context.Days[nextWorking.Date].FirstOrDefault(d =>
                                (d.AppliesToAll || (standardId.HasValue && d.StandardId == standardId.Value)))
                            : await _calendarDayRepository.FirstOrDefaultAsync(d =>
                            d.AcademicCalendarId == calendar.Id &&
                            d.Date.Date == nextWorking.Date &&
                            d.Status == CalendarDayStatus.Active &&
                            (d.AppliesToAll || (standardId.HasValue && d.StandardId == standardId.Value)));
                        
                        bool nextBlocked = nextDay != null && CalendarDayBlockingRules.Blocks(nextDay.DayType, "timetable");
                        if (!nextBlocked)
                        {
                            break;
                        }
                        nextWorking = nextWorking.AddDays(1);
                    }
                    suggestedNextWorking = nextWorking;
                }
            }
        }

        if (!calAllowed)
        {
            conflicts.Add(new ConflictItem(
                "holidayCalendar",
                "critical",
                calMsg ?? "Date is blocked in academic calendar.",
                suggestedNextWorking.HasValue 
                    ? $"Reschedule reservation to next working day: {suggestedNextWorking.Value:yyyy-MM-dd}"
                    : "Select another date."
            ));
            result.HolidayCalendar = new ValidationPass(false, calMsg);
        }

        // 2. Check Maintenance Blocks
        var requestedStart = date.Date.Add(startTime);
        var requestedEnd = date.Date.Add(endTime);

        var maintenanceBlocks = context != null ? context.Maintenance[roomId]
            .Where(m => m.StartDateTime < requestedEnd && m.EndDateTime > requestedStart).ToList() : await _maintenanceRepository.GetListAsync(m =>
            m.RoomId == roomId &&
            m.Status != MaintenanceStatus.Cancelled &&
            m.StartDateTime < requestedEnd &&
            m.EndDateTime > requestedStart
        );

        if (maintenanceBlocks.Any())
        {
            var block = maintenanceBlocks.First();
            conflicts.Add(new ConflictItem(
                "maintenanceCheck",
                "critical",
                $"Room is under {block.Severity} maintenance: '{block.Title}' until {block.EndDateTime:hh:mm tt}.",
                "Select alternative room or reschedule maintenance."
            ));
            result.MaintenanceCheck = new ValidationPass(false, $"Maintenance: {block.Title}");
        }

        // 3. Check Overlapping Room Reservations
        var existingReservations = (context != null ? context.Reservations[roomId].Where(row => row.Date == date.Date).ToList() : await _reservationRepository.GetListAsync(r =>
            r.RoomId == roomId &&
            r.Date == date.Date
        )).Where(r =>
            (!currentReservationId.HasValue || r.Id != currentReservationId.Value) &&
            r.StartTime < endTime &&
            r.EndTime > startTime &&
            (r.Status == RoomReservationStatus.Approved ||
             r.Status == RoomReservationStatus.Active ||
             r.Status == RoomReservationStatus.CheckedIn ||
             r.Status == RoomReservationStatus.PendingApproval)
        ).ToList();

        if (existingReservations.Any())
        {
            var res = existingReservations.First();
            conflicts.Add(new ConflictItem(
                "roomAvailability",
                "critical",
                $"Room is reserved for '{res.Purpose}' ({res.ReservationType}) from {res.StartTime:hh\\:mm} to {res.EndTime:hh\\:mm}.",
                "Choose alternative room or override booking if permitted."
            ));
            result.RoomAvailability = new ValidationPass(false, $"Reserved: {res.Purpose}");
        }

        // 4. Check Timetable Schedule (Weekly recurring slots)
        // 4. Check Timetable Schedule (Weekly recurring slots)
        if (date.DayOfWeek != DayOfWeek.Sunday)
        {
            var timetableDay = GetTimetableDayOfWeek(date.DayOfWeek);

            // Only scan timetable slots if the calendar doesn't block timetable classes on this date
            if (calAllowed)
            {
                var slotOccupations = context != null
                    ? context.Occupations[roomId]
                        .Where(occupation => occupation.StartTime < TimeOnly.FromTimeSpan(endTime) &&
                            occupation.EndTime > TimeOnly.FromTimeSpan(startTime)).ToList()
                    : await _timetableRepository.GetRoomOccupationsAsync([roomId], timetableDay,
                        TimeOnly.FromTimeSpan(startTime), TimeOnly.FromTimeSpan(endTime));

                if (slotOccupations.Any())
                {
                    var firstOccupation = slotOccupations.First();
                    var timetableRoomMessage = await BuildTimetableRoomConflictMessageAsync(
                        firstOccupation, context);

                    conflicts.Add(new ConflictItem(
                        "roomAvailability",
                        "critical",
                        timetableRoomMessage,
                        "Choose another room, change the timetable room allocation, or select a non-overlapping time."
                    ));
                    result.RoomAvailability = new ValidationPass(false, timetableRoomMessage);
                }
            }
        }

        // 5. Check institute setup class-room assignment against class shift timing.
        if (calAllowed && date.DayOfWeek != DayOfWeek.Sunday)
        {
            var classOccupancy = await FindClassShiftOccupancyAsync(
                roomId,
                startTime,
                endTime,
                standardId,
                divisionId,
                context);
            if (classOccupancy != null)
            {
                var conflict = CreateClassShiftConflict(classOccupancy);
                conflicts.Add(conflict);
                result.ClassAvailability = new ValidationPass(false, conflict.Message);
                result.RoomAvailability = new ValidationPass(false, "Class shift assignment conflict.");
            }
        }

        // 6. Check Teacher Double-booking (Warning)
        if (teacherId.HasValue)
        {
            var teacherReservations = context != null
                ? context.TeacherReservations[teacherId.Value].Where(row => row.Date == date.Date).ToList()
                : await _reservationRepository.GetListAsync(r =>
                r.TeacherId == teacherId.Value &&
                r.Date == date.Date
            );
            var teacherReserved = teacherReservations.Any(r =>
                (!currentReservationId.HasValue || r.Id != currentReservationId.Value) &&
                r.StartTime < endTime &&
                r.EndTime > startTime &&
                (r.Status == RoomReservationStatus.Approved ||
                 r.Status == RoomReservationStatus.Active)
            );
            if (teacherReserved)
            {
                conflicts.Add(new ConflictItem(
                    "teacherAvailability",
                    "warning",
                    "Assigned staff is already booked for another room reservation during this period.",
                    "Assign a different teacher/staff member."
                ));
                result.TeacherAvailability = new ValidationPass(false, "Teacher is double-booked.");
            }
        }

        // Compile aggregate severity
        result.Conflicts = conflicts;
        result.CanSave = !conflicts.Any(c => c.Severity == "critical");
        result.RequiresApproval = conflicts.Any(c => c.Severity == "warning");
        result.OverallSeverity = conflicts.Any(c => c.Severity == "critical") ? "critical"
                               : conflicts.Any(c => c.Severity == "warning") ? "warning"
                               : "none";

        return result;
    }

    private async Task<string> BuildTimetableRoomConflictMessageAsync(
        TimetableConflictCandidate occupation,
        RoomConflictReadContext? context)
    {
        var standard = context != null ? context.Standards.GetValueOrDefault(occupation.StandardId)
            : (await ReadRoomStandardsAsync(new[] { occupation.StandardId })).GetValueOrDefault(occupation.StandardId);
        var division = context != null ? context.Divisions.FirstOrDefault(d => d.Id == occupation.DivisionId)
            : (await ReadRoomDivisionsAsync((await _divisionRepository.GetQueryableAsync()).Where(d => d.Id == occupation.DivisionId))).FirstOrDefault();
        var classLabel = $"{standard?.Name ?? "Class"} Division {division?.Name ?? "-"}";

        return
            $"Room is already allocated in the timetable for {classLabel} on {occupation.Day} " +
            $"{occupation.StartTime:HH\\:mm}-{occupation.EndTime:HH\\:mm}.";
    }

    private async Task<ClassShiftOccupancy?> FindClassShiftOccupancyAsync(
        Guid roomId,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? currentStandardId,
        Guid? currentDivisionId,
        RoomConflictReadContext? context)
    {
        RoomConflictRoom room;
        if (context != null) room = context.Rooms[roomId];
        else
        {
            var entity = await _roomRepository.GetAsync(roomId);
            room = new RoomConflictRoom(entity.Id, entity.RoomName);
        }
        var divisions = context != null
            ? context.Divisions.Where(d => d.RoomNumber != null && d.RoomNumber != string.Empty).ToList()
            : await ReadRoomDivisionsAsync((await _divisionRepository.GetQueryableAsync()).Where(d => d.RoomNumber != null && d.RoomNumber != string.Empty));
        var matchedDivisions = divisions
            .Where(d => IsAssignedToRoom(d.RoomNumber!, room))
            .ToList();

        if (!matchedDivisions.Any())
        {
            return null;
        }

        var standardIds = matchedDivisions.Select(d => d.StandardId).Distinct().ToList();
        var standardsById = context?.Standards ?? await ReadRoomStandardsAsync(standardIds);

        foreach (var division in matchedDivisions.OrderBy(d => d.Name))
        {
            if (division.StandardId == currentStandardId && division.Id == currentDivisionId)
            {
                continue;
            }

            if (!standardsById.TryGetValue(division.StandardId, out var standard) || !standard.ShiftStart.HasValue || !standard.ShiftEnd.HasValue)
            {
                continue;
            }

            var shiftStart = standard.ShiftStart.Value.ToTimeSpan();
            var shiftEnd = standard.ShiftEnd.Value.ToTimeSpan();
            if (shiftStart < endTime && shiftEnd > startTime)
            {
                return new ClassShiftOccupancy(
                    standard.Name,
                    division.Name,
                    standard.ShiftName ?? "",
                    shiftStart,
                    shiftEnd);
            }
        }

        return null;
    }

    private static bool IsAssignedToRoom(string roomNumber, RoomConflictRoom room)
    {
        var assignedRoom = roomNumber.Trim();

        if (Guid.TryParse(assignedRoom, out var assignedRoomId) && assignedRoomId == room.Id)
        {
            return true;
        }

        return string.Equals(assignedRoom, room.RoomName, StringComparison.OrdinalIgnoreCase)
               || room.RoomName.EndsWith(assignedRoom, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ClassShiftOccupancy(
        string StandardName,
        string DivisionName,
        string ShiftName,
        TimeSpan StartTime,
        TimeSpan EndTime);

    private sealed record RoomConflictReadContext(
        AcademicCalendar? Calendar,
        ILookup<DateTime, AcademicCalendarDay> Days,
        ILookup<Guid, RoomMaintenanceBlock> Maintenance,
        ILookup<Guid, RoomReservation> Reservations,
        ILookup<Guid, TimetableConflictCandidate> Occupations,
        Dictionary<Guid, RoomConflictRoom> Rooms,
        List<RoomDivisionRead> Divisions,
        Dictionary<Guid, RoomStandardRead> Standards,
        ILookup<Guid, RoomReservation> TeacherReservations);

    private TimetableDayOfWeek GetTimetableDayOfWeek(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => TimetableDayOfWeek.Monday,
            DayOfWeek.Tuesday => TimetableDayOfWeek.Tuesday,
            DayOfWeek.Wednesday => TimetableDayOfWeek.Wednesday,
            DayOfWeek.Thursday => TimetableDayOfWeek.Thursday,
            DayOfWeek.Friday => TimetableDayOfWeek.Friday,
            DayOfWeek.Saturday => TimetableDayOfWeek.Saturday,
            _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek), dayOfWeek, "Sunday is not a valid timetable day.")
        };
    }
}
