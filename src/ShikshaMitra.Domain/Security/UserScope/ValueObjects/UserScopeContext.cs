using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Represents a user's effective scope after resolving all assignments (user-specific + role-based).
/// Used for caching and quick scope checks.
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
    /// Indicates if user has wildcard access to ALL academic years (NULL in assignment).
    /// </summary>
    public bool HasWildcardAcademicYear { get; set; }

    /// <summary>
    /// Indicates if user has wildcard access to ALL boards (NULL in assignment).
    /// </summary>
    public bool HasWildcardBoard { get; set; }

    /// <summary>
    /// Indicates if user has wildcard access to ALL mediums (NULL in assignment).
    /// </summary>
    public bool HasWildcardMedium { get; set; }

    /// <summary>
    /// Indicates if user has wildcard access to ALL medium stages (NULL in assignment).
    /// </summary>
    public bool HasWildcardMediumStage { get; set; }

    /// <summary>
    /// Indicates if user has wildcard access to ALL standards (NULL in assignment).
    /// </summary>
    public bool HasWildcardStandard { get; set; }

    /// <summary>
    /// Indicates if user has wildcard access to ALL divisions (NULL in assignment).
    /// </summary>
    public bool HasWildcardDivision { get; set; }

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
}
