namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Defines hierarchical levels for user scope assignment.
/// Lower values = broader access, Higher values = narrower access.
/// </summary>
public enum ScopeLevel
{
    /// <summary>
    /// Full access to entire organization (all boards, mediums, stages, classes, divisions).
    /// Typically assigned to super admins.
    /// </summary>
    Organization = 0,

    /// <summary>
    /// Access to all boards within a specific academic year.
    /// </summary>
    AcademicYear = 1,

    /// <summary>
    /// Access to all mediums, stages, classes, and divisions under a specific education board.
    /// Example: CBSE Board access.
    /// </summary>
    Board = 2,

    /// <summary>
    /// Access to all stages, classes, and divisions under a specific education medium.
    /// Example: English Medium access.
    /// </summary>
    Medium = 3,

    /// <summary>
    /// Access to all classes and divisions under a specific education medium stage.
    /// Example: Primary Stage access (all primary classes).
    /// </summary>
    MediumStage = 4,

    /// <summary>
    /// Access to all divisions under a specific standard/class.
    /// Example: Class 2 access (all divisions in Class 2).
    /// NULL DivisionId = All divisions in this class.
    /// </summary>
    Standard = 5,

    /// <summary>
    /// Access to a specific division only.
    /// Example: Class 2, Division A only.
    /// Most granular level of access.
    /// </summary>
    Division = 6
}
