using System;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Marker interface for entities that support user scope filtering.
/// Entities implementing this interface will have automatic scope-based filtering applied via EF Core global query filters.
/// </summary>
/// <remarks>
/// The NULL = ALL principle applies:
/// - If EducationBoardId is NULL in UserScopeAssignment, user has access to ALL boards
/// - If DivisionId is NULL in UserScopeAssignment, user has access to ALL divisions in the specified Standard
/// - And so on for other hierarchy levels
/// </remarks>
public interface IUserScopedEntity
{
    /// <summary>
    /// Education Board ID. Used for board-level filtering.
    /// </summary>
    Guid? EducationBoardId { get; }

    /// <summary>
    /// Education Medium ID. Used for medium-level filtering.
    /// </summary>
    Guid? EducationMediumId { get; }

    /// <summary>
    /// Education Medium Stage ID. Used for stage-level filtering (Primary, Secondary, etc.).
    /// </summary>
    Guid? EducationMediumStageId { get; }

    /// <summary>
    /// Standard/Class ID. Used for class-level filtering.
    /// </summary>
    Guid? StandardId { get; }

    /// <summary>
    /// Division ID. Used for division-level filtering (most granular).
    /// </summary>
    Guid? DivisionId { get; }
}
