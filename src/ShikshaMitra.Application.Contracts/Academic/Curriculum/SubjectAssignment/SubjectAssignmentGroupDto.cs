using System;
using System.Collections.Generic;

namespace ShikshaMitra.Academic.Curriculum;

public class SubjectAssignmentGroupDto
{
    public Guid Id { get; set; }
    public SubjectType SubjectType { get; set; }
    public Guid? ElectiveGroupId { get; set; }
    public string? ElectiveGroupName { get; set; }
    public string? Color { get; set; }
    public SubjectStatus Status { get; set; }
    public int TotalSlots { get; set; }
    public int OccupiedSlots { get; set; }
    public int UnoccupiedSlots { get; set; }
    public List<SubjectLiteDto> Subjects { get; set; } = new();

    // ── Enriched fields for world-class table ──────────────────────────
    /// <summary>Setup health score 0-100</summary>
    public int SetupHealthScore { get; set; }

    /// <summary>Number of syllabus chapters added</summary>
    public int ChaptersCount { get; set; }

    /// <summary>Weekly periods allocated for this subject</summary>
    public int WeeklyPeriods { get; set; }

    /// <summary>Primary teacher name (display only)</summary>
    public string? PrimaryTeacherName { get; set; }

    /// <summary>Primary teacher id</summary>
    public string? PrimaryTeacherId { get; set; }

    /// <summary>When this record was last modified</summary>
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>Whether this subject was suggested by AI</summary>
    public bool IsAiSuggested { get; set; }

    /// <summary>Whether copied from previous year</summary>
    public bool IsCopiedFromPreviousYear { get; set; }

    /// <summary>Whether this assignment can be permanently removed.</summary>
    public bool CanDelete { get; set; } = true;

    /// <summary>Reason shown when permanent remove is blocked.</summary>
    public string? DeleteDisabledReason { get; set; }
}
