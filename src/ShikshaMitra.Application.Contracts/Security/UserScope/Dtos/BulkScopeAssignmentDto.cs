using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// DTO for bulk scope assignment to multiple users supporting multiple values at each hierarchical level.
/// Creates assignments for all combinations (cartesian product) of the provided scope parameters.
/// Example: 2 users × 2 boards × 3 standards × 2 divisions = 24 assignments.
/// This operation is transactional and follows an OVERWRITE strategy:
/// - First, deletes ALL existing scope assignments for the specified users
/// - Then, creates the new assignments based on the provided parameters
/// - Either all operations succeed or none are applied (rollback)
/// </summary>
public class BulkScopeAssignmentDto : IValidatableObject
{
    /// <summary>
    /// List of user IDs to assign scope to.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one user ID must be provided.")]
    public List<Guid> UserIds { get; set; } = new();

    /// <summary>
    /// List of Academic Year IDs. NULL or empty = all academic years.
    /// Creates one assignment per academic year (if multiple provided).
    /// </summary>
    public List<Guid>? AcademicYearIds { get; set; }

    /// <summary>
    /// List of Education Board IDs. NULL or empty = all boards.
    /// Creates one assignment per board (if multiple provided).
    /// </summary>
    public List<Guid>? EducationBoardIds { get; set; }

    /// <summary>
    /// List of Education Medium IDs. NULL or empty = all mediums.
    /// Creates one assignment per medium (if multiple provided).
    /// </summary>
    public List<Guid>? EducationMediumIds { get; set; }

    /// <summary>
    /// List of Education Medium Stage IDs. NULL or empty = all stages.
    /// Creates one assignment per stage (if multiple provided).
    /// </summary>
    public List<Guid>? EducationMediumStageIds { get; set; }

    /// <summary>
    /// List of Standard/Class IDs to assign. NULL or empty = all standards.
    /// Creates one assignment per standard (if multiple provided).
    /// </summary>
    public List<Guid>? StandardIds { get; set; }

    /// <summary>
    /// List of Division IDs to assign. NULL or empty = all divisions.
    /// Creates one assignment per division (if multiple provided).
    /// Combines with StandardIds to create standard-division combinations.
    /// </summary>
    public List<Guid>? DivisionIds { get; set; }

    /// <summary>
    /// Description for all bulk assignments.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Validates the DTO input.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Validate that at least one scope parameter is provided
        var hasScopeParams = (AcademicYearIds?.Count > 0) ||
                            (EducationBoardIds?.Count > 0) ||
                            (EducationMediumIds?.Count > 0) ||
                            (EducationMediumStageIds?.Count > 0) ||
                            (StandardIds?.Count > 0) ||
                            (DivisionIds?.Count > 0);

        if (!hasScopeParams)
        {
            yield return new ValidationResult(
                "At least one scope parameter (AcademicYearIds, EducationBoardIds, EducationMediumIds, EducationMediumStageIds, StandardIds, or DivisionIds) must be provided.",
                new[] { nameof(AcademicYearIds), nameof(EducationBoardIds), nameof(EducationMediumIds), 
                       nameof(EducationMediumStageIds), nameof(StandardIds), nameof(DivisionIds) });
        }
    }
}
