using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class RoomMaintenanceBlock : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid RoomId { get; private set; }
    public string Title { get; private set; }
    public string Reason { get; private set; }
    public MaintenanceSeverity Severity { get; private set; }
    public DateTime StartDateTime { get; private set; }
    public DateTime EndDateTime { get; private set; }
    public string? AssignedStaff { get; private set; }
    public string? Remarks { get; private set; }
    public MaintenanceStatus Status { get; private set; }

    private RoomMaintenanceBlock()
    {
        // EF materializes these required columns after this ORM constructor.
        Reason = null!;
        Title = null!;
    }

    public RoomMaintenanceBlock(
        Guid id,
        Guid? tenantId,
        Guid roomId,
        string title,
        string reason,
        MaintenanceSeverity severity,
        DateTime startDateTime,
        DateTime endDateTime,
        string? assignedStaff = null,
        string? remarks = null) : base(id)
    {
        TenantId = tenantId;
        RoomId = roomId;
        Title = title;
        Reason = reason;
        Severity = severity;
        StartDateTime = startDateTime;
        EndDateTime = endDateTime;
        AssignedStaff = assignedStaff;
        Remarks = remarks;
        Status = MaintenanceStatus.Scheduled;
    }

    public void StartMaintenance()
    {
        Status = MaintenanceStatus.InProgress;
    }

    public void CompleteMaintenance(string? remarks = null)
    {
        Status = MaintenanceStatus.Completed;
        if (remarks != null) Remarks = remarks;
    }

    public void CancelMaintenance(string? remarks = null)
    {
        Status = MaintenanceStatus.Cancelled;
        if (remarks != null) Remarks = remarks;
    }
}
