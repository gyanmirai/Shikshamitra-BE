using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.SeatAllocation.Dtos;

// ── Seating plan draft (plan-level state wrapping the per-seat allocation rows) ──

public class SeatingPlanDraftResponseDto
{
    public Guid SeatingPlanId { get; set; }
    public Guid ExamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SeatingPlanScopeType ScopeType { get; set; }
    public List<Guid> ExamSubjectIds { get; set; } = [];
    public List<Guid> SubjectIds { get; set; } = [];
    public List<Guid> RoomIds { get; set; } = [];
    public string AllocationRulesJson { get; set; } = string.Empty;
    public List<Guid> ExcludedStudentIds { get; set; } = [];

    /// <summary>Students flagged as needing special seating (spec §17) — flag only, no reason stored.</summary>
    public List<Guid> SpecialRequirementStudentIds { get; set; } = [];

    /// <summary>Single source of truth counts — same computation used by the list dashboard, so
    /// the list page, workspace header, and this draft never disagree (master spec §5).</summary>
    public int TotalStudents { get; set; }
    public int AllocatedStudents { get; set; }
    public int PendingStudents { get; set; }
    public int AllocationProgress { get; set; }

    public SeatingPlanStatus Status { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LastModifiedTime { get; set; }
}

public class SaveSeatingPlanDraftInputDto
{
    public Guid? SeatingPlanId { get; set; }
    public string? Name { get; set; }
    public SeatingPlanScopeType? ScopeType { get; set; }
    public List<Guid>? ExamSubjectIds { get; set; }
    public List<Guid> RoomIds { get; set; } = [];
    public string AllocationRulesJson { get; set; } = string.Empty;
    public List<Guid> ExcludedStudentIds { get; set; } = [];
    public List<Guid> SpecialRequirementStudentIds { get; set; } = [];

    /// <summary>The <see cref="SeatingPlanDraftResponseDto.LastModifiedTime"/> the client last read.
    /// If it no longer matches the server's value, the save is rejected as a concurrency conflict.</summary>
    public DateTime? LastKnownModifiedTime { get; set; }
}
