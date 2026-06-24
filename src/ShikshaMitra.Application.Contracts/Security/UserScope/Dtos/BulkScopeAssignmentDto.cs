using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// DTO for bulk scope assignment to multiple users using explicit hierarchy path rows.
/// Creates one assignment for each user and each supplied scope row.
/// This operation is transactional and follows an ACADEMIC-YEAR-SCOPED OVERWRITE strategy:
/// - First, deletes existing scope assignments only for the specified users and academic year
/// - Then, creates the new assignments based on the provided scope rows for that year
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
    /// Academic year ID to replace. Required for bulk assignment.
    /// </summary>
    [Required]
    public Guid? AcademicYearId { get; set; }

    /// <summary>
    /// Explicit hierarchy path rows to assign. An empty row represents full academic-year access.
    /// </summary>
    public List<BulkScopeAssignmentScopeDto> Scopes { get; set; } = [];

    /// <summary>
    /// When true, clears existing assignments in the supplied academic years and creates no new scope rows.
    /// Scopes must be empty when this flag is set.
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
        var scopes = Scopes ?? new List<BulkScopeAssignmentScopeDto>();

        if (!AcademicYearId.HasValue || AcademicYearId.Value == Guid.Empty)
        {
            yield return new ValidationResult(
                "An academic year must be provided. Bulk assignment replaces permissions only within the supplied academic year.",
                new[] { nameof(AcademicYearId) });
        }

        if (UserIds?.Any(x => x == Guid.Empty) == true)
        {
            yield return new ValidationResult(
                "User IDs cannot be empty GUIDs.",
                new[] { nameof(UserIds) });
        }

        if (scopes.Any(scope => scope == null || HasEmptyGuid(scope)))
        {
            yield return new ValidationResult(
                "Scope IDs cannot be empty GUIDs.",
                new[] { nameof(Scopes) });
        }

        if (ClearExisting && scopes.Count > 0)
        {
            yield return new ValidationResult(
                "ClearExisting can only be used with an empty scopes array.",
                new[] { nameof(ClearExisting), nameof(Scopes) });
        }

        if (!ClearExisting && scopes.Count == 0)
        {
            yield return new ValidationResult(
                "At least one scope row must be provided when ClearExisting is false. Use an empty scope row for full academic-year access.",
                new[] { nameof(Scopes) });
        }

        foreach (var scope in scopes)
        {
            if (scope == null)
            {
                continue;
            }

            if (scope.EducationMediumId.HasValue && !scope.EducationBoardId.HasValue)
            {
                yield return MissingParent(nameof(scope.EducationMediumId), nameof(scope.EducationBoardId));
            }

            if (scope.EducationMediumStageId.HasValue &&
                (!scope.EducationBoardId.HasValue || !scope.EducationMediumId.HasValue))
            {
                yield return MissingParent(
                    nameof(scope.EducationMediumStageId),
                    $"{nameof(scope.EducationBoardId)}, {nameof(scope.EducationMediumId)}");
            }

            if (scope.StandardId.HasValue &&
                (!scope.EducationBoardId.HasValue ||
                 !scope.EducationMediumId.HasValue ||
                 !scope.EducationMediumStageId.HasValue))
            {
                yield return MissingParent(
                    nameof(scope.StandardId),
                    $"{nameof(scope.EducationBoardId)}, {nameof(scope.EducationMediumId)}, {nameof(scope.EducationMediumStageId)}");
            }

            if (scope.DivisionId.HasValue &&
                (!scope.EducationBoardId.HasValue ||
                 !scope.EducationMediumId.HasValue ||
                 !scope.EducationMediumStageId.HasValue ||
                 !scope.StandardId.HasValue))
            {
                yield return MissingParent(
                    nameof(scope.DivisionId),
                    $"{nameof(scope.EducationBoardId)}, {nameof(scope.EducationMediumId)}, {nameof(scope.EducationMediumStageId)}, {nameof(scope.StandardId)}");
            }
        }
    }

    private static bool HasEmptyGuid(BulkScopeAssignmentScopeDto scope) =>
        scope.EducationBoardId == Guid.Empty ||
        scope.EducationMediumId == Guid.Empty ||
        scope.EducationMediumStageId == Guid.Empty ||
        scope.StandardId == Guid.Empty ||
        scope.DivisionId == Guid.Empty;

    private static ValidationResult MissingParent(string childMember, string parentMembers) =>
        new(
            $"{childMember} requires parent scope ID(s): {parentMembers}.",
            new[] { nameof(Scopes) });
}

/// <summary>
/// A single explicit hierarchy path row within one academic year.
/// </summary>
public class BulkScopeAssignmentScopeDto
{
    public Guid? EducationBoardId { get; set; }

    public Guid? EducationMediumId { get; set; }

    public Guid? EducationMediumStageId { get; set; }

    public Guid? StandardId { get; set; }

    public Guid? DivisionId { get; set; }
}
