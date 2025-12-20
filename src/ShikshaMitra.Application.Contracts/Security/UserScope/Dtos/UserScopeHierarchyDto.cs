using System;
using System.Collections.Generic;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Frontend-optimized DTO containing user's scope as a fully resolved hierarchy.
/// Eliminates ambiguity by explicitly marking access levels and showing complete structure.
/// </summary>
public class UserScopeHierarchyDto
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
    /// If true, all other hierarchy data represents "full access".
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Fully resolved hierarchy of accessible academic years, boards, mediums, etc.
    /// Each level explicitly shows what is accessible.
    /// When HasFullAccess is true at any level, nested collections will be empty for performance optimization.
    /// </summary>
    public List<AcademicYearScopeNode> AccessibleHierarchy { get; set; } = new();

    /// <summary>
    /// Whether the scope was loaded from cache.
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// Timestamp when this hierarchy was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Represents an academic year node in the hierarchy.
/// </summary>
public class AcademicYearScopeNode
{
    public Guid AcademicYearId { get; set; }
    public string AcademicYearName { get; set; } = string.Empty;

    /// <summary>
    /// True if user has access to ALL boards under this academic year.
    /// If true, Boards list still contains all boards for display purposes.
    /// </summary>
    public bool HasFullBoardAccess { get; set; }

    /// <summary>
    /// List of accessible education boards under this academic year.
    /// </summary>
    public List<BoardScopeNode> Boards { get; set; } = new();
}

/// <summary>
/// Represents an education board node in the hierarchy.
/// </summary>
public class BoardScopeNode
{
    public Guid BoardId { get; set; }
    public string BoardName { get; set; } = string.Empty;

    /// <summary>
    /// True if user has access to this board (considering parent hierarchy).
    /// </summary>
    public bool HasAccess { get; set; }

    /// <summary>
    /// True if user has access to ALL mediums under this board.
    /// </summary>
    public bool HasFullMediumAccess { get; set; }

    /// <summary>
    /// List of education mediums under this board.
    /// </summary>
    public List<MediumScopeNode> Mediums { get; set; } = new();
}

/// <summary>
/// Represents an education medium node in the hierarchy.
/// </summary>
public class MediumScopeNode
{
    public Guid MediumId { get; set; }
    public string MediumName { get; set; } = string.Empty;

    /// <summary>
    /// True if user has access to this medium.
    /// </summary>
    public bool HasAccess { get; set; }

    /// <summary>
    /// True if user has access to ALL medium stages under this medium.
    /// </summary>
    public bool HasFullMediumStageAccess { get; set; }

    /// <summary>
    /// List of education medium stages under this medium.
    /// </summary>
    public List<MediumStageScopeNode> MediumStages { get; set; } = new();
}

/// <summary>
/// Represents an education medium stage node in the hierarchy.
/// </summary>
public class MediumStageScopeNode
{
    public Guid MediumStageId { get; set; }
    public string MediumStageName { get; set; } = string.Empty;

    /// <summary>
    /// True if user has access to this medium stage.
    /// </summary>
    public bool HasAccess { get; set; }

    /// <summary>
    /// True if user has access to ALL standards/classes under this medium stage.
    /// </summary>
    public bool HasFullStandardAccess { get; set; }

    /// <summary>
    /// List of standards/classes under this medium stage.
    /// </summary>
    public List<StandardScopeNode> Standards { get; set; } = new();
}

/// <summary>
/// Represents a standard/class node in the hierarchy.
/// </summary>
public class StandardScopeNode
{
    public Guid StandardId { get; set; }
    public string StandardName { get; set; } = string.Empty;

    /// <summary>
    /// True if user has access to this standard.
    /// </summary>
    public bool HasAccess { get; set; }

    /// <summary>
    /// True if user has access to ALL divisions under this standard.
    /// When true, Divisions list still shows all divisions for UI purposes.
    /// </summary>
    public bool HasFullDivisionAccess { get; set; }

    /// <summary>
    /// List of divisions under this standard.
    /// </summary>
    public List<DivisionScopeNode> Divisions { get; set; } = new();
}

/// <summary>
/// Represents a division node in the hierarchy.
/// </summary>
public class DivisionScopeNode
{
    public Guid DivisionId { get; set; }
    public string DivisionName { get; set; } = string.Empty;

    /// <summary>
    /// True if user has access to this division.
    /// </summary>
    public bool HasAccess { get; set; }

    /// <summary>
    /// Optional: Room number for additional context.
    /// </summary>
    public string? RoomNumber { get; set; }
}
