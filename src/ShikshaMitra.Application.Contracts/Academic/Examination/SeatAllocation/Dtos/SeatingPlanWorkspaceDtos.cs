using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

/// <summary>
/// Authoritative read model for the guided examination-seating workflow.
/// The examination is the top-level record. Each plan can own one subject or
/// a user-selected group of subjects that reuse the same rooms and seat map.
/// </summary>
public class SeatingPlanWorkspaceDto
{
    public Guid ExamId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public Guid? SelectedSeatingPlanId { get; set; }
    public string? Version { get; set; }
    public List<SeatingPlanSubjectDto> AvailableSubjects { get; set; } = [];
    public List<SeatingPlanGroupDto> Plans { get; set; } = [];
    public ExaminationSeatingReadinessDto Readiness { get; set; } = new();

    /// <summary>
    /// Legacy session projection retained for older clients. New guided clients
    /// use <see cref="Plans"/> and let the user choose the subject grouping.
    /// </summary>
    public List<SeatingPlanSessionDto> Sessions { get; set; } = [];
}

public class SeatingPlanGroupDto
{
    public Guid SeatingPlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SeatingPlanScopeType ScopeType { get; set; }
    public SeatingPlanStatus Status { get; set; }
    public bool IsLocked { get; set; }
    public bool HasCompleteSchedule { get; set; }
    public bool CanConfigure { get; set; }
    public int ScheduleCount { get; set; }
    public List<SeatingPlanSubjectDto> Subjects { get; set; } = [];
    public List<DivisionLiteDto> Divisions { get; set; } = [];
    public List<SeatingPlanRoomOptionDto> AvailableRooms { get; set; } = [];
    public List<Guid> SelectedRoomIds { get; set; } = [];
    public int TotalStudents { get; set; }
    public int AllocatedStudents { get; set; }
    public int PendingStudents { get; set; }
    public int RoomsUsed { get; set; }
    public int UsableCapacity { get; set; }
    public int EmptySeats { get; set; }
    public int Utilization { get; set; }
    public List<SeatingWorkspaceIssueDto> Issues { get; set; } = [];
}

public class SeatingPlanSessionDto
{
    public string SessionKey { get; set; } = string.Empty;
    public DateTime? ExamDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool HasCompleteSchedule { get; set; }
    public List<SeatingPlanSubjectDto> Subjects { get; set; } = [];
    public List<DivisionLiteDto> Divisions { get; set; } = [];
    public List<SeatingPlanRoomOptionDto> AvailableRooms { get; set; } = [];
    public List<Guid> SelectedRoomIds { get; set; } = [];

    public Guid? SeatingPlanId { get; set; }
    public string? SeatingPlanName { get; set; }
    public SeatingPlanStatus Status { get; set; }
    public bool IsLocked { get; set; }
    public bool RequiresMigration { get; set; }
    public bool CanPrepare { get; set; }

    public int TotalStudents { get; set; }
    public int AllocatedStudents { get; set; }
    public int PendingStudents { get; set; }
    public int RoomsUsed { get; set; }
    public int UsableCapacity { get; set; }
    public int EmptySeats { get; set; }
    public int Utilization { get; set; }
    public List<SeatingWorkspaceIssueDto> Issues { get; set; } = [];
}

public class SeatingPlanRoomOptionDto
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int UsableCapacity { get; set; }
    public int DisabledSeats { get; set; }
}

public class SeatingWorkspaceIssueDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionCode { get; set; }
    public string? ActionLabel { get; set; }
    public Guid? RelatedSeatingPlanId { get; set; }
}

public class PrepareSessionSeatingPlanInputDto
{
    public string? Name { get; set; }
    public List<Guid> ExamSubjectIds { get; set; } = [];
}

public class PrepareSeatingPlanInputDto
{
    public string? Name { get; set; }
    public List<Guid> ExamSubjectIds { get; set; } = [];
}
