using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

/// <summary>
/// Audit trail for seating-plan lifecycle events (spec §21). Deliberately scoped to the
/// events that change plan-level state (draft save, finalize, publish, unlock) rather than
/// per-seat drag/drop noise — per-seat history lives in the undo/redo stack on the client
/// during editing, this table is the durable record once a plan-level action commits.
/// </summary>
public class SeatingPlanAuditLog : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid ExamId { get; private set; }
    public SeatingPlanAuditAction Action { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string? ActorNameSnapshot { get; private set; }
    public string? Reason { get; private set; }

    /// <summary>Snapshot of assignments (studentId/roomId/seatNumber) at the time of this action, for version history / print regeneration.</summary>
    public string? AssignmentsSnapshotJson { get; private set; }
    public DateTime ChangedAt { get; private set; }

    private SeatingPlanAuditLog() { }

    public SeatingPlanAuditLog(
        Guid id,
        Guid? tenantId,
        Guid examId,
        SeatingPlanAuditAction action,
        Guid actorUserId,
        string? actorNameSnapshot,
        string? reason,
        string? assignmentsSnapshotJson) : base(id)
    {
        TenantId = tenantId;
        ExamId = examId;
        Action = action;
        ActorUserId = actorUserId;
        ActorNameSnapshot = actorNameSnapshot;
        Reason = reason;
        AssignmentsSnapshotJson = assignmentsSnapshotJson;
        ChangedAt = DateTime.UtcNow;
    }
}
