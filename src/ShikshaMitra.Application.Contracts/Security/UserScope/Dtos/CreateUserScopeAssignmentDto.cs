using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// DTO for creating a new user scope assignment.
/// </summary>
public class CreateUserScopeAssignmentDto
{
    /// <summary>
    /// User ID for user-specific scope. Either UserId OR RoleName must be provided.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Role name for role-based scope. Either UserId OR RoleName must be provided.
    /// </summary>
    [MaxLength(256)]
    public string? RoleName { get; set; }

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
    /// Standard/Class ID. NULL = all classes in the scope.
    /// </summary>
    public Guid? StandardId { get; set; }

    /// <summary>
    /// Division ID. NULL = all divisions in the class.
    /// </summary>
    public Guid? DivisionId { get; set; }

    /// <summary>
    /// Description explaining why this scope is being assigned.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
}
