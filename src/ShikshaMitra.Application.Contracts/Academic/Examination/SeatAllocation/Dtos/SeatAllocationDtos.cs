using System;
using System.Collections.Generic;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

// ── Seat Layout ──────────────────────────────────────────────────────────────

public class SeatLayoutItemDto
{
    public int SeatNumber { get; set; }
    public Guid? StudentId { get; set; }
    public string? StudentName { get; set; }
    public string StandardName { get; set; } = string.Empty;
    public int? RollNo { get; set; }
    public string? RoomNumber { get; set; }
    public bool IsRoomAssign { get; set; }
    public Guid? BenchId { get; set; }
    public bool IsExternalExamOccupied { get; set; }
}

public class SeatLayoutResponseDto
{
    public Guid? RoomId { get; set; }
    public RoomDto? Room { get; set; }
    public int TotalSeats { get; set; }
    public int AllocatedCount { get; set; }
    public List<SeatLayoutItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public int CurrentPage { get; set; }
}

// ── Students for allocation ──────────────────────────────────────────────────

public class SeatStudentDto
{
    public int SeatNumber { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StandardName { get; set; } = string.Empty;
    public int RollNo { get; set; }
    public Guid StudentId { get; set; }
    public bool IsSeatAllocate { get; set; }
}

public class SeatStudentPagedResponseDto
{
    public List<SeatStudentDto> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

// ── Divisions for exam ───────────────────────────────────────────────────────

public class DivisionLiteDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SeatAllocationDivisionsResponseDto
{
    public Guid ExamId { get; set; }
    public List<DivisionLiteDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int PageSize { get; set; }
    public int CurrentPage { get; set; }
}

// ── Dashboard table ──────────────────────────────────────────────────────────

public class SeatAllocationSummaryDto
{
    public Guid ExamId { get; set; }
    public string ExamName { get; set; } = string.Empty;
    public string ExamTypeName { get; set; } = string.Empty;
    public string StandardName { get; set; } = string.Empty;
    public List<string> Divisions { get; set; } = [];
    public List<SeatAllocationDivisionProgressDto> DivisionProgress { get; set; } = [];
    public int TotalStudents { get; set; }
    public int AllocatedStudents { get; set; }
    public int PendingStudents { get; set; }
    public int RoomsUsed { get; set; }
    public int SeatingPlanCount { get; set; }
    public int SubjectCount { get; set; }
    public int PlannedSubjectCount { get; set; }
    public List<string> SubjectNames { get; set; } = [];
    public List<SeatAllocationSessionSummaryDto> Sessions { get; set; } = [];
    /// <summary>See <see cref="SeatingPlanStatus"/>.</summary>
    public int Status { get; set; }
}

/// <summary>
/// One scheduled examination time slot in the seating-plan inbox. All progress, capacity and
/// lifecycle values are calculated from the same exact session plan so the list and workspace
/// cannot disagree.
/// </summary>
public class SeatAllocationSessionSummaryDto
{
    public string SessionKey { get; set; } = string.Empty;
    public Guid? SeatingPlanId { get; set; }
    public string? SeatingPlanName { get; set; }
    public DateTime? ExamDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<string> SubjectNames { get; set; } = [];
    public List<string> Divisions { get; set; } = [];
    public List<SeatAllocationDivisionProgressDto> DivisionProgress { get; set; } = [];
    public int TotalStudents { get; set; }
    public int AllocatedStudents { get; set; }
    public int PendingStudents { get; set; }
    public int RoomsUsed { get; set; }
    public int UsableCapacity { get; set; }
    public int EmptySeats { get; set; }
    public int Utilization { get; set; }
    public int Status { get; set; }
    public bool HasCompleteSchedule { get; set; }
    public bool CanPrepare { get; set; }
    public bool RequiresMigration { get; set; }
    public int BlockingIssueCount { get; set; }
}

public class SeatAllocationDivisionProgressDto
{
    public Guid DivisionId { get; set; }
    public string DivisionName { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int AllocatedStudents { get; set; }
    public int PendingStudents { get; set; }
    public int Progress { get; set; }
}

public class SeatAllocationDashboardResponseDto
{
    public List<SeatAllocationSummaryDto> Items { get; set; } = [];
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
}

// ── Rooms for exam + division ────────────────────────────────────────────────

public class ExamRoomDto
{
    public Guid Id { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int AllocatedSeats { get; set; }
    public int AvailableSeats { get; set; }
    public bool IsAllocationCompleted { get; set; }
}

public class ExamRoomPagedResponseDto
{
    public List<ExamRoomDto> Items { get; set; } = [];
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

// ── Save allocation ──────────────────────────────────────────────────────────

public class SaveSeatStudentDto
{
    public Guid StudentId { get; set; }
    public int SeatNumber { get; set; }
    public Guid? BenchId { get; set; }
    public bool IsRoomAssign { get; set; }
}

public class SaveSeatAllocationRequestDto
{
    public Guid? SeatingPlanId { get; set; }
    public Guid RoomId { get; set; }
    public List<SaveSeatStudentDto> Students { get; set; } = [];
}

public class SaveSeatAllocationResponseDto
{
    public Guid ExamId { get; set; }
    public Guid RoomId { get; set; }
    public int SavedCount { get; set; }
}

public class SaveSeatingPlanBulkRoomDto
{
    public Guid RoomId { get; set; }
    public List<SaveSeatStudentDto> Students { get; set; } = [];
}

public class SaveSeatingPlanBulkInputDto : SaveSeatingPlanDraftInputDto
{
    /// <summary>Only rooms loaded/edited by the client are replaced; omitted rooms remain unchanged.</summary>
    public List<SaveSeatingPlanBulkRoomDto> Rooms { get; set; } = [];
}
