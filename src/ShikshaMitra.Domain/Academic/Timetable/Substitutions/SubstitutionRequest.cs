using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

public class SubstitutionRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid? AcademicYearId { get; private set; }
    public DateTime Date { get; private set; }
    public string Day { get; private set; } = string.Empty;
    public string Period { get; private set; } = string.Empty;
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public Guid? StandardId { get; private set; }
    public string ClassName { get; private set; } = string.Empty;
    public Guid? DivisionId { get; private set; }
    public string DivisionName { get; private set; } = string.Empty;
    public Guid? TimetableId { get; private set; }
    public Guid? TimetableSlotId { get; private set; }
    public Guid? TimetableSlotSubjectId { get; private set; }
    public Guid? SubjectId { get; private set; }
    public string SubjectName { get; private set; } = string.Empty;
    public string DepartmentName { get; private set; } = string.Empty;
    public Guid? OriginalTeacherId { get; private set; }
    public string OriginalTeacherName { get; private set; } = string.Empty;
    public Guid? SubstituteTeacherId { get; private set; }
    public string? SubstituteTeacherName { get; private set; }
    public Guid? OriginalRoomId { get; private set; }
    public string RoomName { get; private set; } = string.Empty;
    public Guid? AssignedRoomId { get; private set; }
    public string? AssignedRoomName { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public SubstitutionStatus Status { get; private set; }
    public SubstitutionPriority Priority { get; private set; }
    public SubstitutionAcknowledgementStatus AcknowledgementStatus { get; private set; }
    public string NotificationStatus { get; private set; } = "Not Sent";
    public SubstitutionSource Source { get; private set; }
    public SubstitutionConflictStatus ConflictStatus { get; private set; }
    public string? ConflictReason { get; private set; }
    public int? SuggestionScore { get; private set; }
    public string WorkloadStatus { get; private set; } = "Low";
    public bool AcknowledgementRequired { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Remarks { get; private set; }

    private SubstitutionRequest()
    {
    }

    public SubstitutionRequest(
        Guid id,
        Guid? tenantId,
        DateTime date,
        string period,
        TimeOnly startTime,
        TimeOnly endTime,
        string className,
        string divisionName,
        string subjectName,
        string originalTeacherName,
        string roomName,
        string reason,
        SubstitutionPriority priority,
        SubstitutionSource source,
        bool acknowledgementRequired,
        Guid? academicYearId = null,
        Guid? standardId = null,
        Guid? divisionId = null,
        Guid? timetableId = null,
        Guid? timetableSlotId = null,
        Guid? timetableSlotSubjectId = null,
        Guid? subjectId = null,
        Guid? originalTeacherId = null,
        Guid? originalRoomId = null,
        string? departmentName = null,
        string? remarks = null)
        : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        Day = date.ToString("dddd");
        Period = period;
        StartTime = startTime;
        EndTime = endTime;
        StandardId = standardId;
        ClassName = className;
        DivisionId = divisionId;
        DivisionName = divisionName;
        TimetableId = timetableId;
        TimetableSlotId = timetableSlotId;
        TimetableSlotSubjectId = timetableSlotSubjectId;
        SubjectId = subjectId;
        SubjectName = subjectName;
        DepartmentName = departmentName ?? subjectName;
        OriginalTeacherId = originalTeacherId;
        OriginalTeacherName = originalTeacherName;
        OriginalRoomId = originalRoomId;
        RoomName = roomName;
        AssignedRoomId = originalRoomId;
        AssignedRoomName = roomName;
        Reason = reason;
        Priority = priority;
        Source = source;
        AcknowledgementRequired = acknowledgementRequired;
        Status = SubstitutionStatus.Pending;
        AcknowledgementStatus = SubstitutionAcknowledgementStatus.NotSent;
        ConflictStatus = SubstitutionConflictStatus.Clear;
        Remarks = remarks;
    }

    public void Assign(
        Guid? substituteTeacherId,
        string substituteTeacherName,
        Guid? assignedRoomId,
        string? assignedRoomName,
        int? suggestionScore,
        string workloadStatus,
        SubstitutionConflictStatus conflictStatus,
        string? conflictReason,
        bool sendNotification)
    {
        SubstituteTeacherId = substituteTeacherId;
        SubstituteTeacherName = substituteTeacherName;
        AssignedRoomId = assignedRoomId ?? OriginalRoomId;
        AssignedRoomName = string.IsNullOrWhiteSpace(assignedRoomName) ? RoomName : assignedRoomName;
        SuggestionScore = suggestionScore;
        WorkloadStatus = workloadStatus;
        ConflictStatus = conflictStatus;
        ConflictReason = conflictReason;
        Status = conflictStatus == SubstitutionConflictStatus.HardBlock
            ? SubstitutionStatus.Conflict
            : AcknowledgementRequired
                ? SubstitutionStatus.WaitingAck
                : SubstitutionStatus.Assigned;
        AcknowledgementStatus = sendNotification
            ? SubstitutionAcknowledgementStatus.Sent
            : SubstitutionAcknowledgementStatus.NotSent;
        NotificationStatus = sendNotification ? "Queued" : "Not Sent";
    }

    public void SetAcademicYear(Guid? academicYearId)
    {
        AcademicYearId = academicYearId;
    }

    public void UpdateDetails(
        DateTime date,
        string period,
        TimeOnly startTime,
        TimeOnly endTime,
        string className,
        string divisionName,
        string subjectName,
        string originalTeacherName,
        string roomName,
        string reason,
        SubstitutionPriority priority,
        SubstitutionSource source,
        bool acknowledgementRequired,
        Guid? academicYearId,
        Guid? standardId,
        Guid? divisionId,
        Guid? timetableId,
        Guid? timetableSlotId,
        Guid? timetableSlotSubjectId,
        Guid? subjectId,
        Guid? originalTeacherId,
        Guid? originalRoomId,
        string? departmentName,
        string? remarks)
    {
        AcademicYearId = academicYearId;
        Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        Day = date.ToString("dddd");
        Period = period;
        StartTime = startTime;
        EndTime = endTime;
        StandardId = standardId;
        ClassName = className;
        DivisionId = divisionId;
        DivisionName = divisionName;
        TimetableId = timetableId;
        TimetableSlotId = timetableSlotId;
        TimetableSlotSubjectId = timetableSlotSubjectId;
        SubjectId = subjectId;
        SubjectName = subjectName;
        DepartmentName = departmentName ?? subjectName;
        OriginalTeacherId = originalTeacherId;
        OriginalTeacherName = originalTeacherName;
        OriginalRoomId = originalRoomId;
        RoomName = roomName;
        AssignedRoomId ??= originalRoomId;
        AssignedRoomName = string.IsNullOrWhiteSpace(AssignedRoomName) ? roomName : AssignedRoomName;
        Reason = reason;
        Priority = priority;
        Source = source;
        AcknowledgementRequired = acknowledgementRequired;
        Remarks = remarks;
    }

    public void MarkNotificationSent()
    {
        NotificationStatus = "Sent";
        if (AcknowledgementStatus == SubstitutionAcknowledgementStatus.NotSent)
        {
            AcknowledgementStatus = SubstitutionAcknowledgementStatus.Sent;
        }
    }

    public void Acknowledge(string? remarks)
    {
        AcknowledgementStatus = SubstitutionAcknowledgementStatus.Accepted;
        Status = SubstitutionStatus.Acknowledged;
        AcknowledgedAt = DateTime.UtcNow;
        Remarks = remarks;
    }

    public void RequestChange(string reason)
    {
        AcknowledgementStatus = SubstitutionAcknowledgementStatus.ChangeRequested;
        Status = SubstitutionStatus.Pending;
        ConflictReason = reason;
        Remarks = reason;
    }

    public void Reject(string reason)
    {
        AcknowledgementStatus = SubstitutionAcknowledgementStatus.Rejected;
        Status = SubstitutionStatus.Rejected;
        SubstituteTeacherId = null;
        SubstituteTeacherName = null;
        ConflictReason = reason;
        Remarks = reason;
    }

    public void Complete()
    {
        Status = SubstitutionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        if (AcknowledgementStatus != SubstitutionAcknowledgementStatus.Accepted)
        {
            AcknowledgementStatus = SubstitutionAcknowledgementStatus.Accepted;
        }
    }

    public void Cancel(string? reason)
    {
        Status = SubstitutionStatus.Cancelled;
        ConflictReason = reason;
    }
}
