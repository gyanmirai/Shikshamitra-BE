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

public class RoomConflictService : DomainService
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
        var result = new ConflictCheckResult();
        var conflicts = new List<ConflictItem>();

        // 1. Check Academic Calendar
        var calendar = await _calendarRepository.FirstOrDefaultAsync(c =>
            c.Status == CalendarStatus.Published &&
            date.Date >= c.StartDate.Date &&
            date.Date <= c.EndDate.Date);

        bool calAllowed = true;
        string? calMsg = null;
        DateTime? suggestedNextWorking = null;

        if (calendar != null)
        {
            var day = await _calendarDayRepository.FirstOrDefaultAsync(d =>
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
                        var nextDay = await _calendarDayRepository.FirstOrDefaultAsync(d =>
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

        var maintenanceBlocks = await _maintenanceRepository.GetListAsync(m =>
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
        var existingReservations = await _reservationRepository.GetListAsync(r =>
            r.RoomId == roomId &&
            r.Date == date.Date &&
            r.Id != currentReservationId &&
            (r.Status == RoomReservationStatus.Approved ||
             r.Status == RoomReservationStatus.Active ||
             r.Status == RoomReservationStatus.CheckedIn ||
             r.Status == RoomReservationStatus.PendingApproval) &&
            r.StartTime < endTime &&
            r.EndTime > startTime
        );

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
                var timetables = (await _timetableRepository.GetAllWithSlotsAsync())
                    .Where(t => t.Status != TimetableStatus.NotCreated)
                    .ToList();
                var slotOccupations = timetables
                    .SelectMany(t => t.Slots)
                    .Where(s => s.Day == timetableDay && s.StartTime < TimeOnly.FromTimeSpan(endTime) && s.EndTime > TimeOnly.FromTimeSpan(startTime))
                    .SelectMany(s => s.Subjects.Select(subject => new { Slot = s, Subject = subject }))
                    .Where(row => row.Subject.RoomId == roomId)
                    .ToList();

                if (slotOccupations.Any())
                {
                    var firstOccupation = slotOccupations.First();
                    var timetable = timetables.FirstOrDefault(t => t.Id == firstOccupation.Slot.TimetableId);
                    var timetableRoomMessage = await BuildTimetableRoomConflictMessageAsync(
                        timetable,
                        firstOccupation.Slot,
                        date);

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
                divisionId);
            if (classOccupancy != null)
            {
                var shiftName = string.IsNullOrWhiteSpace(classOccupancy.ShiftName)
                    ? "configured class shift"
                    : classOccupancy.ShiftName;

                var message =
                    $"Room is occupied by {classOccupancy.StandardName} Division {classOccupancy.DivisionName} " +
                    $"from {classOccupancy.StartTime:hh\\:mm} to {classOccupancy.EndTime:hh\\:mm} based on institute setup shift '{shiftName}'.";

                conflicts.Add(new ConflictItem(
                    "classAvailability",
                    "critical",
                    message,
                    "Choose another room or change the division room assignment / class shift in institute setup."
                ));
                result.ClassAvailability = new ValidationPass(false, message);
                result.RoomAvailability = new ValidationPass(false, "Class shift assignment conflict.");
            }
        }

        // 6. Check Teacher Double-booking (Warning)
        if (teacherId.HasValue)
        {
            var teacherReserved = await _reservationRepository.AnyAsync(r =>
                r.TeacherId == teacherId.Value &&
                r.Date == date.Date &&
                r.Id != currentReservationId &&
                (r.Status == RoomReservationStatus.Approved || r.Status == RoomReservationStatus.Active) &&
                r.StartTime < endTime &&
                r.EndTime > startTime
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
        Timetables.Timetable? timetable,
        TimetableSlot slot,
        DateTime date)
    {
        var classLabel = "another class";

        if (timetable != null)
        {
            var standard = await _standardRepository.FindAsync(timetable.StandardId);
            var division = await _divisionRepository.FindAsync(timetable.DivisionId);
            classLabel = $"{standard?.Name ?? "Class"} Division {division?.Name ?? "-"}";
        }

        return
            $"Room is already allocated in the timetable for {classLabel} on {date:dddd} " +
            $"{slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm}.";
    }

    private async Task<ClassShiftOccupancy?> FindClassShiftOccupancyAsync(
        Guid roomId,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? currentStandardId,
        Guid? currentDivisionId)
    {
        var room = await _roomRepository.GetAsync(roomId);
        var divisions = await _divisionRepository.GetListAsync(d => d.RoomNumber != null && d.RoomNumber != string.Empty);
        var matchedDivisions = divisions
            .Where(d => IsAssignedToRoom(d.RoomNumber, room))
            .ToList();

        if (!matchedDivisions.Any())
        {
            return null;
        }

        var standardIds = matchedDivisions.Select(d => d.StandardId).Distinct().ToList();
        var standards = await _standardRepository.GetListAsync(s => standardIds.Contains(s.Id));
        var standardsById = standards.ToDictionary(s => s.Id);

        foreach (var division in matchedDivisions.OrderBy(d => d.Name))
        {
            if (division.StandardId == currentStandardId && division.Id == currentDivisionId)
            {
                continue;
            }

            if (!standardsById.TryGetValue(division.StandardId, out var standard) || standard.Shift == null)
            {
                continue;
            }

            var shiftStart = standard.Shift.StartTime.ToTimeSpan();
            var shiftEnd = standard.Shift.EndTime.ToTimeSpan();
            if (shiftStart < endTime && shiftEnd > startTime)
            {
                return new ClassShiftOccupancy(
                    standard.Name,
                    division.Name,
                    standard.Shift.ShiftName,
                    shiftStart,
                    shiftEnd);
            }
        }

        return null;
    }

    private static bool IsAssignedToRoom(string roomNumber, Room room)
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
