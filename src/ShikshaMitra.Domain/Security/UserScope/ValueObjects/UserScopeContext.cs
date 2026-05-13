using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Represents a user's effective authorization scope after resolving all assignments (user-specific + role-based).
/// It is optimized for quick access checks and may expand parent-level assignments into lower-level wildcard flags.
/// It is not an exact mirror of the hierarchy nodes selected in the UI.
/// </summary>
public class UserScopeContext
{
    /// <summary>
    /// User ID for whom this scope context is calculated.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy support.
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Indicates if user is an administrator with full access (bypasses all scope checks).
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// List of academic year IDs the user has access to.
    /// Empty list = No access. NULL values in assignments = ALL years.
    /// </summary>
    public List<Guid> AcademicYearIds { get; set; } = new();

    /// <summary>
    /// List of education board IDs the user has access to.
    /// Empty list = No access. NULL in assignments = ALL boards.
    /// </summary>
    public List<Guid> BoardIds { get; set; } = new();

    /// <summary>
    /// List of education medium IDs the user has access to.
    /// Empty list = No access. NULL in assignments = ALL mediums.
    /// </summary>
    public List<Guid> MediumIds { get; set; } = new();

    /// <summary>
    /// List of education medium stage IDs the user has access to.
    /// Empty list = No access. NULL in assignments = ALL stages.
    /// </summary>
    public List<Guid> MediumStageIds { get; set; } = new();

    /// <summary>
    /// List of standard/class IDs the user has access to.
    /// Empty list = No access. NULL in assignments = ALL classes.
    /// </summary>
    public List<Guid> StandardIds { get; set; } = new();

    /// <summary>
    /// List of division IDs the user has access to.
    /// Empty list = No access. NULL in assignments = ALL divisions.
    /// </summary>
    public List<Guid> DivisionIds { get; set; } = new();

    /// <summary>
    /// Timestamp when this scope context was calculated.
    /// Used for cache validation.
    /// </summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates if this scope context was loaded from cache.
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// Indicates if user effectively has wildcard access to all academic years.
    /// </summary>
    public bool HasWildcardAcademicYear { get; set; }

    /// <summary>
    /// Indicates if user effectively has wildcard access to all boards in the applicable parent scope.
    /// </summary>
    public bool HasWildcardBoard { get; set; }

    /// <summary>
    /// Indicates if user effectively has wildcard access to all mediums in the applicable parent scope.
    /// </summary>
    public bool HasWildcardMedium { get; set; }

    /// <summary>
    /// Indicates if user effectively has wildcard access to all medium stages in the applicable parent scope.
    /// </summary>
    public bool HasWildcardMediumStage { get; set; }

    /// <summary>
    /// Indicates if user effectively has wildcard access to all standards in the applicable parent scope.
    /// </summary>
    public bool HasWildcardStandard { get; set; }

    /// <summary>
    /// Indicates if user effectively has wildcard access to all divisions in the applicable parent scope.
    /// </summary>
    public bool HasWildcardDivision { get; set; }

    /// <summary>
    /// Academic year IDs where the user effectively has access to every board and lower child scope.
    /// Empty with <see cref="HasWildcardAcademicYear"/> means all years.
    /// </summary>
    public List<Guid> WildcardBoardAcademicYearIds { get; set; } = new();

    /// <summary>
    /// Board IDs where the user effectively has access to every medium and lower child scope.
    /// </summary>
    public List<Guid> WildcardMediumBoardIds { get; set; } = new();

    /// <summary>
    /// Medium IDs where the user effectively has access to every medium stage and lower child scope.
    /// </summary>
    public List<Guid> WildcardMediumStageMediumIds { get; set; } = new();

    /// <summary>
    /// Medium stage IDs where the user effectively has access to every standard and lower child scope.
    /// </summary>
    public List<Guid> WildcardStandardMediumStageIds { get; set; } = new();

    /// <summary>
    /// Standard IDs where the user effectively has access to every division.
    /// </summary>
    public List<Guid> WildcardDivisionStandardIds { get; set; } = new();

    /// <summary>
    /// Checks if user has any scope assigned (at least one non-empty list, wildcard, or IsAdmin).
    /// </summary>
    public bool HasAnyAccess =>
        IsAdmin ||
        AcademicYearIds.Any() || HasWildcardAcademicYear ||
        BoardIds.Any() || HasWildcardBoard ||
        MediumIds.Any() || HasWildcardMedium ||
        MediumStageIds.Any() || HasWildcardMediumStage ||
        StandardIds.Any() || HasWildcardStandard ||
        DivisionIds.Any() || HasWildcardDivision;

    public bool HasTenantWideScope() =>
        IsAdmin || HasWildcardAcademicYear;

    public bool HasAcademicYearWideScope(Guid? academicYearId)
    {
        if (HasTenantWideScope())
            return true;

        return academicYearId.HasValue &&
            WildcardBoardAcademicYearIds.Contains(academicYearId.Value);
    }

    public bool CanAccessMediumStage(
        Guid? stageId,
        Guid? mediumId = null,
        Guid? boardId = null,
        Guid? academicYearId = null)
    {
        if (HasTenantWideScope())
            return true;

        if (stageId.HasValue &&
            (MediumStageIds.Contains(stageId.Value) ||
             WildcardStandardMediumStageIds.Contains(stageId.Value)))
        {
            return true;
        }

        if (mediumId.HasValue &&
            (MediumIds.Contains(mediumId.Value) ||
             WildcardMediumStageMediumIds.Contains(mediumId.Value)))
        {
            return true;
        }

        if (boardId.HasValue &&
            (BoardIds.Contains(boardId.Value) ||
             WildcardMediumBoardIds.Contains(boardId.Value)))
        {
            return true;
        }

        return HasAcademicYearWideScope(academicYearId);
    }

    public bool RequiresConcreteMediumStageFiltering() =>
        !HasTenantWideScope();

    internal static UserScopeContext CreateEffective(
        Guid userId,
        Guid? tenantId,
        IEnumerable<UserScopeAssignment> assignments)
    {
        var scope = new UserScopeContext
        {
            UserId = userId,
            TenantId = tenantId,
            IsAdmin = false,
            CalculatedAt = DateTime.UtcNow,
            FromCache = false
        };

        foreach (var assignment in assignments.Where(a => a.IsActive))
        {
            if (assignment.AcademicYearId.HasValue)
                scope.AcademicYearIds.Add(assignment.AcademicYearId.Value);

            if (assignment.EducationBoardId.HasValue)
                scope.BoardIds.Add(assignment.EducationBoardId.Value);

            if (assignment.EducationMediumId.HasValue)
                scope.MediumIds.Add(assignment.EducationMediumId.Value);

            if (assignment.EducationMediumStageId.HasValue)
                scope.MediumStageIds.Add(assignment.EducationMediumStageId.Value);

            if (assignment.StandardId.HasValue)
                scope.StandardIds.Add(assignment.StandardId.Value);

            if (assignment.DivisionId.HasValue)
                scope.DivisionIds.Add(assignment.DivisionId.Value);

            if (assignment.GrantsAllAcademicYears())
            {
                scope.HasWildcardAcademicYear = true;
                scope.HasWildcardBoard = true;
                scope.HasWildcardMedium = true;
                scope.HasWildcardMediumStage = true;
                scope.HasWildcardStandard = true;
                scope.HasWildcardDivision = true;
                continue;
            }

            if (assignment.GrantsAllBoardsInAcademicYear())
            {
                scope.WildcardBoardAcademicYearIds.Add(assignment.AcademicYearId!.Value);
                scope.HasWildcardBoard = true;
                scope.HasWildcardMedium = true;
                scope.HasWildcardMediumStage = true;
                scope.HasWildcardStandard = true;
                scope.HasWildcardDivision = true;
                continue;
            }

            if (assignment.GrantsAllMediumsInBoard())
            {
                scope.WildcardMediumBoardIds.Add(assignment.EducationBoardId!.Value);
                scope.HasWildcardMedium = true;
                scope.HasWildcardMediumStage = true;
                scope.HasWildcardStandard = true;
                scope.HasWildcardDivision = true;
                continue;
            }

            if (assignment.GrantsAllMediumStagesInMedium())
            {
                scope.WildcardMediumStageMediumIds.Add(assignment.EducationMediumId!.Value);
                scope.HasWildcardMediumStage = true;
                scope.HasWildcardStandard = true;
                scope.HasWildcardDivision = true;
                continue;
            }

            if (assignment.GrantsAllStandardsInMediumStage())
            {
                scope.WildcardStandardMediumStageIds.Add(assignment.EducationMediumStageId!.Value);
                scope.HasWildcardStandard = true;
                scope.HasWildcardDivision = true;
                continue;
            }

            if (assignment.GrantsAllDivisionsInStandard())
            {
                scope.WildcardDivisionStandardIds.Add(assignment.StandardId!.Value);
                scope.HasWildcardDivision = true;
            }
        }

        scope.AcademicYearIds = scope.AcademicYearIds.Distinct().ToList();
        scope.BoardIds = scope.BoardIds.Distinct().ToList();
        scope.MediumIds = scope.MediumIds.Distinct().ToList();
        scope.MediumStageIds = scope.MediumStageIds.Distinct().ToList();
        scope.StandardIds = scope.StandardIds.Distinct().ToList();
        scope.DivisionIds = scope.DivisionIds.Distinct().ToList();
        scope.WildcardBoardAcademicYearIds = scope.WildcardBoardAcademicYearIds.Distinct().ToList();
        scope.WildcardMediumBoardIds = scope.WildcardMediumBoardIds.Distinct().ToList();
        scope.WildcardMediumStageMediumIds = scope.WildcardMediumStageMediumIds.Distinct().ToList();
        scope.WildcardStandardMediumStageIds = scope.WildcardStandardMediumStageIds.Distinct().ToList();
        scope.WildcardDivisionStandardIds = scope.WildcardDivisionStandardIds.Distinct().ToList();

        return scope;
    }
}
