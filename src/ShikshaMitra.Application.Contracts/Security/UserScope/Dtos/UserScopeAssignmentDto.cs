using System;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// DTO for user scope assignment details.
/// </summary>
public class UserScopeAssignmentDto
{
    /// <summary>
    /// Unique identifier for the scope assignment.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User ID (if user-specific scope).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Role name (if role-based scope).
    /// </summary>
    public string? RoleName { get; set; }

    /// <summary>
    /// Academic Year ID. NULL = all years.
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
    /// Standard/Class ID. NULL = all classes.
    /// </summary>
    public Guid? StandardId { get; set; }

    /// <summary>
    /// Division ID. NULL = all divisions.
    /// </summary>
    public Guid? DivisionId { get; set; }

    /// <summary>
    /// Description explaining this scope assignment.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates if this scope is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Scope level calculated from the IDs.
    /// </summary>
    public ScopeLevel ScopeLevel { get; set; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreationTime { get; set; }
}
