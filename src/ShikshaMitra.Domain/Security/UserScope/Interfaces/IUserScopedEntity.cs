using System;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Marker interface for entities that support user scope filtering.
/// Entities implementing this interface can be filtered with <see cref="UserScopeQueryExtensions"/>.
/// </summary>
/// <remarks>
/// UserScopeContext applies effective wildcard access:
/// - A parent-level assignment grants access to children inside that parent scope.
/// - A null parent on a child-specific assignment does not grant access to every parent.
/// - A null DivisionId with a StandardId grants all divisions in that standard.
/// </remarks>
public interface IUserScopedEntity
{
    /// <summary>
    /// Academic Year ID. Used for academic-year-level filtering (top of hierarchy).
    /// </summary>
    Guid? AcademicYearId { get; }

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
