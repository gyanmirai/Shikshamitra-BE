using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// DTO for creating a new user scope assignment.
/// </summary>
public class CreateUserScopeAssignmentDto : IValidatableObject
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

    /// <summary>
    /// Validates that exactly one of UserId or RoleName is provided (XOR constraint).
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasUserId = UserId.HasValue;
        var hasRoleName = !string.IsNullOrWhiteSpace(RoleName);

        // Both are missing
        if (!hasUserId && !hasRoleName)
        {
            yield return new ValidationResult(
                "Either UserId or RoleName must be provided.",
                new[] { nameof(UserId), nameof(RoleName) });
        }
        // Both are provided
        else if (hasUserId && hasRoleName)
        {
            yield return new ValidationResult(
                "Either UserId or RoleName must be provided, but not both.",
                new[] { nameof(UserId), nameof(RoleName) });
        }
    }
}
