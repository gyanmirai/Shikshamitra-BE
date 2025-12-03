using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// DTO for bulk scope assignment to multiple users.
/// </summary>
public class BulkScopeAssignmentDto
{
    /// <summary>
    /// List of user IDs to assign scope to.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one user ID must be provided.")]
    public List<Guid> UserIds { get; set; } = new();

    /// <summary>
    /// Academic Year ID. NULL = all academic years.
    /// </summary>
    public Guid? AcademicYearId { get; set; }

    /// <summary>
    /// Education Board ID. NULL = all boards.
    /// </summary>
    public Guid? EducationBoardId { get; set; }

    /// <summary>
    /// Education Medium ID. NULL = all mediums.
    /// </summary>
    public Guid? EducationMediumId { get; set; }

    /// <summary>
    /// Education Medium Stage ID. NULL = all stages.
    /// </summary>
    public Guid? EducationMediumStageId { get; set; }

    /// <summary>
    /// Standard/Class ID to assign. Required for bulk assignment.
    /// </summary>
    public Guid? StandardId { get; set; }

    /// <summary>
    /// Division IDs to assign. If null, assigns all divisions in the standard.
    /// If provided, creates multiple assignments (one per division).
    /// </summary>
    public List<Guid>? DivisionIds { get; set; }

    /// <summary>
    /// Description for all bulk assignments.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
}
