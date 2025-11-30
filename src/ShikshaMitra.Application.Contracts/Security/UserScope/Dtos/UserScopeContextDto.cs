using System;
using System.Collections.Generic;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// DTO representing user's effective scope (what they can access).
/// </summary>
public class UserScopeContextDto
{
    /// <summary>
    /// List of academic year IDs accessible by the user.
    /// </summary>
    public List<Guid> AcademicYearIds { get; set; } = new();

    /// <summary>
    /// List of education board IDs accessible by the user.
    /// </summary>
    public List<Guid> BoardIds { get; set; } = new();

    /// <summary>
    /// List of education medium IDs accessible by the user.
    /// </summary>
    public List<Guid> MediumIds { get; set; } = new();

    /// <summary>
    /// List of education medium stage IDs accessible by the user.
    /// </summary>
    public List<Guid> MediumStageIds { get; set; } = new();

    /// <summary>
    /// List of standard/class IDs accessible by the user.
    /// </summary>
    public List<Guid> StandardIds { get; set; } = new();

    /// <summary>
    /// List of division IDs accessible by the user.
    /// </summary>
    public List<Guid> DivisionIds { get; set; } = new();

    /// <summary>
    /// Timestamp when this scope was calculated.
    /// </summary>
    public DateTime CalculatedAt { get; set; }

    /// <summary>
    /// Total accessible divisions count.
    /// </summary>
    public int AccessibleDivisionCount => DivisionIds.Count;

    /// <summary>
    /// Total accessible standards/classes count.
    /// </summary>
    public int AccessibleStandardCount => StandardIds.Count;
}
