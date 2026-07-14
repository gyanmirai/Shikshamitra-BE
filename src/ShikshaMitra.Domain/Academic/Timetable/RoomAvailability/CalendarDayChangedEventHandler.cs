using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Calendar;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class CalendarDayChangedEventHandler 
    : ILocalEventHandler<EntityCreatedEventData<AcademicCalendarDay>>,
      ILocalEventHandler<EntityUpdatedEventData<AcademicCalendarDay>>,
      ITransientDependency
{
    private readonly IRepository<RoomReservation, Guid> _reservationRepository;
    private readonly IRepository<CalendarImpactLog, Guid> _impactRepository;

    public CalendarDayChangedEventHandler(
        IRepository<RoomReservation, Guid> reservationRepository,
        IRepository<CalendarImpactLog, Guid> impactRepository)
    {
        _reservationRepository = reservationRepository;
        _impactRepository = impactRepository;
    }

    public async Task HandleEventAsync(EntityCreatedEventData<AcademicCalendarDay> eventData)
    {
        await ProcessCalendarImpactAsync(eventData.Entity);
    }

    public async Task HandleEventAsync(EntityUpdatedEventData<AcademicCalendarDay> eventData)
    {
        await ProcessCalendarImpactAsync(eventData.Entity);
    }

    private async Task ProcessCalendarImpactAsync(AcademicCalendarDay day)
    {
        if (day.Status != CalendarDayStatus.Active) return;
        if (!CalendarDayBlockingRules.BlocksTimetable(day.DayType)) return;

        // Query active reservations overlapping the blocked date
        var affectedReservations = await _reservationRepository.GetListAsync(r =>
            r.Date == day.Date.Date &&
            (r.Status == RoomReservationStatus.Approved ||
             r.Status == RoomReservationStatus.Active ||
             r.Status == RoomReservationStatus.PendingApproval)
        );

        if (!day.AppliesToAll)
        {
            affectedReservations = affectedReservations
                .Where(r => r.StandardId == day.StandardId && 
                           (!day.DivisionId.HasValue || r.DivisionId == day.DivisionId))
                .ToList();
        }

        foreach (var reservation in affectedReservations)
        {
            if (day.DayType == DayType.EmergencyClosure || day.DayType == DayType.Holiday)
            {
                reservation.Cancel(Guid.Empty, $"Cancelled automatically: Overlaps with Calendar Event '{day.Title}' ({day.DayType})");
                await _reservationRepository.UpdateAsync(reservation);

                // Add to calendar impact logs for tracking
                var impact = new CalendarImpactLog(
                    Guid.NewGuid(),
                    day.TenantId,
                    day.Id,
                    CalendarImpactType.TimetableSlot,
                    $"Room Reservation Overlap: {reservation.Purpose}",
                    day.Date,
                    $"Room Reservation ID {reservation.Id} auto-cancelled due to Calendar Day Block: {day.Title}"
                );
                impact.Resolve(Guid.Empty, "Reservation cancelled automatically by System.");
                await _impactRepository.InsertAsync(impact);
            }
        }
    }
}
