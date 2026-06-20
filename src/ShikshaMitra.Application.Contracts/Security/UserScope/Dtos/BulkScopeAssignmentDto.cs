using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// DTO for bulk scope assignment to multiple users supporting multiple values at each hierarchical level.
/// Creates assignments for all combinations (cartesian product) of the provided scope parameters.
/// Example: 2 users × 2 boards × 3 standards × 2 divisions = 24 assignments.
/// This operation is transactional and follows an ACADEMIC-YEAR-SCOPED OVERWRITE strategy:
/// - First, deletes existing scope assignments only for the specified users and academic years
/// - Then, creates the new assignments based on the provided parameters for those years
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
    /// List of Academic Year IDs to replace. Required for bulk assignment.
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
    /// When true, clears existing assignments in the supplied academic years and creates no new scope rows.
    /// All scope ID arrays must be null or empty when this flag is set.
    /// </summary>
    public bool ClearExisting { get; set; }

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
        if (AcademicYearIds == null || AcademicYearIds.Count == 0)
        {
            yield return new ValidationResult(
                "At least one academic year must be provided. Bulk assignment replaces permissions only within the supplied academic year scope.",
                new[] { nameof(AcademicYearIds) });
        }

        if (ClearExisting && HasAnyScopeIds())
        {
            yield return new ValidationResult(
                "ClearExisting can only be used with empty scope ID arrays.",
                new[] { nameof(ClearExisting), nameof(EducationBoardIds), nameof(EducationMediumIds),
                       nameof(EducationMediumStageIds), nameof(StandardIds), nameof(DivisionIds) });
        }
    }

    private bool HasAnyScopeIds() =>
        (EducationBoardIds?.Count > 0) ||
        (EducationMediumIds?.Count > 0) ||
        (EducationMediumStageIds?.Count > 0) ||
        (StandardIds?.Count > 0) ||
        (DivisionIds?.Count > 0);
}
