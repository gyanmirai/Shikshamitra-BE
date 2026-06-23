using System;
using System.Collections.Generic;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Response DTO for bulk scope assignment operations.
/// Provides information about the transactional bulk assignment operation with academic-year-scoped overwrite strategy.
/// </summary>
public class BulkScopeAssignmentResultDto
{
    /// <summary>
    /// Number of existing scope assignments that were deleted before creating new ones.
    /// </summary>
    public int DeletedAssignments { get; set; }

    /// <summary>
    /// Total number of new scope assignments successfully created.
    /// If the operation fails, this will be 0 as it's all-or-nothing.
    /// </summary>
    public int AssignmentsCreated { get; set; }

    /// <summary>
    /// Total number of assignment rows requested (Users × scope rows).
    /// </summary>
    public int TotalCombinations { get; set; }

    /// <summary>
    /// List of users whose scope assignments were modified (deleted old + created new).
    /// Empty if the operation failed.
    /// </summary>
    public List<Guid> AffectedUserIds { get; set; } = new();

    /// <summary>
    /// Summary message describing the result of the bulk operation.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
