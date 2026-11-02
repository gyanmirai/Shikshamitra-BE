using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class RoomAuditLog : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid RoomId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public string Action { get; private set; } // E.g., "Created", "Approved", "Cancelled"
    public Guid PerformedByUserId { get; private set; }
    public string UserName { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string? OldValue { get; private set; } // JSON
    public string? NewValue { get; private set; } // JSON
    public string? Remarks { get; private set; }

    private RoomAuditLog()
    {
        // EF materializes these required columns after this ORM constructor.
        Action = null!;
        UserName = null!;
    }

    public RoomAuditLog(
        Guid id,
        Guid? tenantId,
        Guid roomId,
        Guid? reservationId,
        string action,
        Guid userId,
        string userName,
        string? oldValue = null,
        string? newValue = null,
        string? remarks = null) : base(id)
    {
        TenantId = tenantId;
        RoomId = roomId;
        ReservationId = reservationId;
        Action = action;
        PerformedByUserId = userId;
        UserName = userName;
        Timestamp = DateTime.UtcNow;
        OldValue = oldValue;
        NewValue = newValue;
        Remarks = remarks;
    }
}
