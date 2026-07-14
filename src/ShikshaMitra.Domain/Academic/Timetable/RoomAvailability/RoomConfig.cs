using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class RoomConfig : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public bool ReservationRequiresApproval { get; set; }
    public bool AuditoriumRequiresApproval { get; set; }
    public bool OutsideHoursRequiresApproval { get; set; }
    public bool AllowTeacherDirectBooking { get; set; }
    public bool AllowConflictOverride { get; set; }
    public int NoShowGracePeriodMinutes { get; set; }
    public bool AutoReleaseNoShow { get; set; }
    public int MinimumBookingMinutes { get; set; }
    public int MaximumBookingMinutes { get; set; }
    public bool AllowRecurringReservation { get; set; }
    public bool AllowBulkReservation { get; set; }
    public int NotifyBeforeMinutes { get; set; }

    public RoomConfig() { }

    public RoomConfig(Guid id, Guid? tenantId) : base(id)
    {
        TenantId = tenantId;
        ReservationRequiresApproval = true;
        AuditoriumRequiresApproval = true;
        OutsideHoursRequiresApproval = true;
        AllowTeacherDirectBooking = false;
        AllowConflictOverride = false;
        NoShowGracePeriodMinutes = 15;
        AutoReleaseNoShow = true;
        MinimumBookingMinutes = 15;
        MaximumBookingMinutes = 360;
        AllowRecurringReservation = true;
        AllowBulkReservation = true;
        NotifyBeforeMinutes = 10;
    }
}
