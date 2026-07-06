using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class RoomConflictService : DomainService
{
    private readonly IRepository<RoomReservation, Guid> _reservationRepository;
    private readonly IRepository<RoomMaintenanceBlock, Guid> _maintenanceRepository;
    private readonly IRepository<Timetables.Timetable, Guid> _timetableRepository;
    private readonly IRepository<AcademicCalendar, Guid> _calendarRepository;
    private readonly IRepository<AcademicCalendarDay, Guid> _calendarDayRepository;

    public RoomConflictService(
        IRepository<RoomReservation, Guid> reservationRepository,
        IRepository<RoomMaintenanceBlock, Guid> maintenanceRepository,
        IRepository<Timetables.Timetable, Guid> timetableRepository,
        IRepository<AcademicCalendar, Guid> calendarRepository,
        IRepository<AcademicCalendarDay, Guid> calendarDayRepository)
    {
        _reservationRepository = reservationRepository;
        _maintenanceRepository = maintenanceRepository;
        _timetableRepository = timetableRepository;
        _calendarRepository = calendarRepository;
        _calendarDayRepository = calendarDayRepository;
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
                var timetables = await _timetableRepository.GetListAsync(t => t.Status == TimetableStatus.Published);
                var slotOccupations = timetables
                    .SelectMany(t => t.Slots)
                    .Where(s => s.Day == timetableDay && s.StartTime < TimeOnly.FromTimeSpan(endTime) && s.EndTime > TimeOnly.FromTimeSpan(startTime))
                    .SelectMany(s => s.Subjects)
                    .Where(subj => subj.RoomId == roomId)
                    .ToList();

                if (slotOccupations.Any())
                {
                    conflicts.Add(new ConflictItem(
                        "roomAvailability",
                        "critical",
                        "Room is pre-allocated in Timetable for recurring lectures in this slot.",
                        "Timetable allocation conflicts must be overridden or alternate room allocated."
                    ));
                    result.RoomAvailability = new ValidationPass(false, "Timetable assignment conflict.");
                }
            }
        }

        // 5. Check Teacher Double-booking (Warning)
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
