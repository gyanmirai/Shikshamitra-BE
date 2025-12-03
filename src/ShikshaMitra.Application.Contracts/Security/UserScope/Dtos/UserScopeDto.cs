using System;
using System.Collections.Generic;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// DTO containing user's complete scope information.
/// </summary>
public class UserScopeDto
{
    /// <summary>
    /// User ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User's display name.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Indicates if user has admin privileges (bypasses all scope checks).
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// List of all scope assignments for this user (both user-specific and role-based).
    /// </summary>
    public List<UserScopeAssignmentDto> Assignments { get; set; } = new();

    /// <summary>
    /// Effective scope calculated from all assignments.
    /// Contains all accessible board IDs, medium IDs, etc.
    /// </summary>
    public UserScopeContextDto EffectiveScope { get; set; } = new();

    /// <summary>
    /// Total number of scope assignments.
    /// </summary>
    public int TotalAssignments => Assignments.Count;

    /// <summary>
    /// Whether the scope was loaded from cache.
    /// </summary>
    public bool FromCache { get; set; }
}
