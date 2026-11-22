using System;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

public class RoomReservation : FullAuditedAggregateRoot<Guid>, IMultiTenant, IUserScopedEntity
{
    public Guid? TenantId { get; private set; }
    public Guid RoomId { get; private set; }
    public DateTime Date { get; private set; } // Date portion only (yyyy-MM-dd)
    public TimeSpan StartTime { get; private set; } // Time component
    public TimeSpan EndTime { get; private set; } // Time component
    public string Purpose { get; private set; }
    public RoomReservationType ReservationType { get; private set; }
    public string? ReservedFor { get; private set; } // E.g. "Grade 9-A", "Science Olympiad Staff"
    public Guid? StandardId { get; private set; }
    public Guid? DivisionId { get; private set; }
    public Guid? SubjectId { get; private set; }
    public Guid? TeacherId { get; private set; }
    public RoomReservationStatus Status { get; private set; }
    
    public bool IsRecurring { get; private set; }
    public Guid? RecurringConfigId { get; private set; } // Links dates belonging to the same series
    
    public string? Remarks { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? CancellationReason { get; private set; }
    
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? CheckedInAt { get; private set; }
    public DateTime? CheckedOutAt { get; private set; }

    Guid? IUserScopedEntity.AcademicYearId => null;
    Guid? IUserScopedEntity.EducationBoardId => null;
    Guid? IUserScopedEntity.EducationMediumId => null;
    Guid? IUserScopedEntity.EducationMediumStageId => null;
    Guid? IUserScopedEntity.StandardId => StandardId;
    Guid? IUserScopedEntity.DivisionId => DivisionId;

    private RoomReservation()
    {
        // EF materializes these required columns after this ORM constructor.
        Purpose = null!;
    }

    public RoomReservation(
        Guid id,
        Guid? tenantId,
        Guid roomId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        string purpose,
        RoomReservationType reservationType,
        Guid? teacherId = null,
        Guid? standardId = null,
        Guid? divisionId = null,
        Guid? subjectId = null,
        string? reservedFor = null,
        string? remarks = null,
        bool isRecurring = false,
        Guid? recurringConfigId = null) : base(id)
    {
        TenantId = tenantId;
        RoomId = roomId;
        Date = date.Date;
        StartTime = startTime;
        EndTime = endTime;
        Purpose = purpose;
        ReservationType = reservationType;
        TeacherId = teacherId;
        StandardId = standardId;
        DivisionId = divisionId;
        SubjectId = subjectId;
        ReservedFor = reservedFor;
        Remarks = remarks;
        IsRecurring = isRecurring;
        RecurringConfigId = recurringConfigId;
        Status = RoomReservationStatus.PendingApproval;
    }

    public void Update(
        Guid roomId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        string purpose,
        RoomReservationType reservationType,
        Guid? teacherId = null,
        Guid? standardId = null,
        Guid? divisionId = null,
        Guid? subjectId = null,
        string? reservedFor = null,
        string? remarks = null)
    {
        var bookingChanged = RoomId != roomId || Date != date.Date || StartTime != startTime || EndTime != endTime ||
            TeacherId != teacherId || StandardId != standardId || DivisionId != divisionId;
        var previousTeacher = TeacherId;
        RoomId = roomId;
        Date = date.Date;
        StartTime = startTime;
        EndTime = endTime;
        Purpose = purpose;
        ReservationType = reservationType;
        TeacherId = teacherId;
        StandardId = standardId;
        DivisionId = divisionId;
        SubjectId = subjectId;
        ReservedFor = reservedFor;
        Remarks = remarks;
        if (!bookingChanged) return;
        if (IsConfirmed(Status))
            RecordNotice(AcademicNotificationEventKeys.RoomChanged, previousTeacher);
        else if (Status == RoomReservationStatus.PendingApproval)
            RequestApprovalNotice();
        else
            RoomReservationNoticeRevision.Forget(this);
    }

    public void RequestApprovalNotice()
    {
        if (Status == RoomReservationStatus.PendingApproval)
            RecordNotice(AcademicNotificationEventKeys.RoomApprovalRequested);
    }

    public void Approve(Guid userId, bool notify = true)
    {
        var changed = !IsConfirmed(Status);
        Status = RoomReservationStatus.Approved;
        ApprovedByUserId = userId;
        ApprovedAt = DateTime.UtcNow;
        if (changed) RecordNotice(AcademicNotificationEventKeys.RoomApproved, notify: notify);
    }

    public void Reject(Guid userId, string reason, bool notify = true)
    {
        var changed = Status != RoomReservationStatus.Rejected;
        Status = RoomReservationStatus.Rejected;
        RejectionReason = reason;
        Remarks = reason;
        ApprovedByUserId = userId;
        ApprovedAt = DateTime.UtcNow;
        if (changed) RecordNotice(AcademicNotificationEventKeys.RoomRejected, notify: notify);
    }

    public void Cancel(Guid userId, string reason, bool notify = true)
    {
        var wasConfirmed = IsConfirmed(Status);
        Status = RoomReservationStatus.Cancelled;
        CancellationReason = reason;
        if (wasConfirmed) RecordNotice(AcademicNotificationEventKeys.RoomCancelled, notify: notify);
    }

    public static bool IsConfirmed(RoomReservationStatus status) =>
        status is RoomReservationStatus.Approved or RoomReservationStatus.Active or RoomReservationStatus.CheckedIn;

    private void RecordNotice(string eventKey, Guid? previousTeacherId = null, bool notify = true)
    {
        if (!TenantId.HasValue) return;
        var revision = RoomReservationNoticeRevision.Record(this, eventKey, previousTeacherId);
        if (notify) AddLocalEvent(new RoomReservationNotification(TenantId, Id, eventKey, revision.Revision));
    }

    public void CheckIn()
    {
        Status = RoomReservationStatus.CheckedIn;
        CheckedInAt = DateTime.UtcNow;
    }

    public void CheckOut()
    {
        Status = RoomReservationStatus.Completed;
        CheckedOutAt = DateTime.UtcNow;
    }

    public void MarkNoShow()
    {
        Status = RoomReservationStatus.NoShow;
    }
}
