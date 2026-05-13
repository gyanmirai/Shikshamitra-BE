using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Security.UserScope;

/// <summary>
/// Aggregate root for user scope assignments.
/// Defines what data a user or role can access based on hierarchical scope (Board → Medium → Stage → Class → Division).
/// </summary>
/// <remarks>
/// Core Principle: NULL = ALL at that level
/// - If DivisionId is NULL but StandardId is set → User has access to ALL divisions in that class
/// - If StandardId is NULL but MediumStageId is set → User has access to ALL classes in that stage
/// - And so on up the hierarchy
///
/// Multiple rows are created for specific entities (e.g., multiple divisions require multiple rows).
/// </remarks>
public class UserScopeAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant, ISoftDelete
{
    /// <summary>
    /// Tenant ID for multi-tenancy support.
    /// </summary>
    public Guid? TenantId { get; private set; }

    /// <summary>
    /// User ID for user-specific scope assignment.
    /// Either UserId OR RoleName must be set (XOR constraint).
    /// </summary>
    public Guid? UserId { get; private set; }

    /// <summary>
    /// Role name for role-based scope assignment.
    /// Either UserId OR RoleName must be set (XOR constraint).
    /// </summary>
    public string? RoleName { get; private set; }

    #region Scope Hierarchy (NULL = ALL at that level)

    /// <summary>
    /// Academic Year ID. NULL = All academic years.
    /// </summary>
    public Guid? AcademicYearId { get; private set; }

    /// <summary>
    /// Education Board ID. NULL = All boards.
    /// Example: CBSE, State Board, etc.
    /// </summary>
    public Guid? EducationBoardId { get; private set; }

    /// <summary>
    /// Education Medium ID. NULL = All mediums.
    /// Example: English, Hindi, etc.
    /// </summary>
    public Guid? EducationMediumId { get; private set; }

    /// <summary>
    /// Education Medium Stage ID. NULL = All stages.
    /// Example: Primary, Secondary, Higher Secondary.
    /// </summary>
    public Guid? EducationMediumStageId { get; private set; }

    /// <summary>
    /// Standard/Class ID. NULL = All classes in the specified scope.
    /// Example: Class 1, Class 2, etc.
    /// </summary>
    public Guid? StandardId { get; private set; }

    /// <summary>
    /// Division ID. NULL = All divisions in the specified class.
    /// Example: Division A, Division B, etc.
    /// </summary>
    public Guid? DivisionId { get; private set; }

    #endregion

    /// <summary>
    /// Description explaining why this scope was assigned.
    /// Example: "Primary teacher for Class 2, Division A", "Principal - full access".
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Indicates if this scope assignment is currently active.
    /// Inactive assignments are ignored during scope resolution.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Soft delete flag (ABP standard).
    /// </summary>
    public bool IsDeleted { get; set; }

    // Private constructor for ORM
    private UserScopeAssignment()
    {
    }

    /// <summary>
    /// Creates a new user scope assignment.
    /// </summary>
    /// <param name="id">Unique identifier</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="userId">User ID (set this OR roleName)</param>
    /// <param name="roleName">Role name (set this OR userId)</param>
    /// <param name="academicYearId">Academic year ID (NULL = all years)</param>
    /// <param name="educationBoardId">Board ID (NULL = all boards)</param>
    /// <param name="educationMediumId">Medium ID (NULL = all mediums)</param>
    /// <param name="educationMediumStageId">Stage ID (NULL = all stages)</param>
    /// <param name="standardId">Class ID (NULL = all classes)</param>
    /// <param name="divisionId">Division ID (NULL = all divisions)</param>
    /// <param name="description">Reason for this assignment</param>
    public UserScopeAssignment(
        Guid id,
        Guid? tenantId,
        Guid? userId = null,
        string? roleName = null,
        Guid? academicYearId = null,
        Guid? educationBoardId = null,
        Guid? educationMediumId = null,
        Guid? educationMediumStageId = null,
        Guid? standardId = null,
        Guid? divisionId = null,
        string? description = null
    ) : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        RoleName = roleName;
        AcademicYearId = academicYearId;
        EducationBoardId = educationBoardId;
        EducationMediumId = educationMediumId;
        EducationMediumStageId = educationMediumStageId;
        StandardId = standardId;
        DivisionId = divisionId;
        Description = description;
        IsActive = true;

        Validate();
    }

    /// <summary>
    /// Validates the scope assignment.
    /// </summary>
    private void Validate()
    {
        // XOR constraint: Either UserId OR RoleName must be set, not both
        if ((UserId.HasValue && !string.IsNullOrWhiteSpace(RoleName)) ||
            (!UserId.HasValue && string.IsNullOrWhiteSpace(RoleName)))
        {
            throw new BusinessException(
                code: "UserScope:InvalidAssignment",
                message: "UserScopeAssignment must have either UserId OR RoleName, not both or neither."
            );
        }

        // Validate description length if provided
        if (!string.IsNullOrWhiteSpace(Description) && Description.Length > 500)
        {
            throw new BusinessException(
                code: "UserScope:DescriptionTooLong",
                message: "Description cannot exceed 500 characters."
            );
        }
    }

    /// <summary>
    /// Activates this scope assignment.
    /// </summary>
    public UserScopeAssignment Activate()
    {
        IsActive = true;
        return this;
    }

    /// <summary>
    /// Deactivates this scope assignment.
    /// </summary>
    public UserScopeAssignment Deactivate()
    {
        IsActive = false;
        return this;
    }

    /// <summary>
    /// Updates the description.
    /// </summary>
    public UserScopeAssignment UpdateDescription(string? description)
    {
        if (!string.IsNullOrWhiteSpace(description) && description.Length > 500)
        {
            throw new BusinessException(
                code: "UserScope:DescriptionTooLong",
                message: "Description cannot exceed 500 characters."
            );
        }

        Description = description;
        return this;
    }

    /// <summary>
    /// Determines the scope level based on which IDs are set.
    /// </summary>
    public ScopeLevel GetScopeLevel()
    {
        if (DivisionId.HasValue) return ScopeLevel.Division;
        if (StandardId.HasValue) return ScopeLevel.Standard;
        if (EducationMediumStageId.HasValue) return ScopeLevel.MediumStage;
        if (EducationMediumId.HasValue) return ScopeLevel.Medium;
        if (EducationBoardId.HasValue) return ScopeLevel.Board;
        if (AcademicYearId.HasValue) return ScopeLevel.AcademicYear;
        return ScopeLevel.Organization;
    }

    public bool GrantsAllAcademicYears() =>
        !AcademicYearId.HasValue
        && !EducationBoardId.HasValue
        && !EducationMediumId.HasValue
        && !EducationMediumStageId.HasValue
        && !StandardId.HasValue
        && !DivisionId.HasValue;

    public bool GrantsAllBoardsInAcademicYear() =>
        AcademicYearId.HasValue
        && !EducationBoardId.HasValue
        && !EducationMediumId.HasValue
        && !EducationMediumStageId.HasValue
        && !StandardId.HasValue
        && !DivisionId.HasValue;

    public bool GrantsAllMediumsInBoard() =>
        EducationBoardId.HasValue
        && !EducationMediumId.HasValue
        && !EducationMediumStageId.HasValue
        && !StandardId.HasValue
        && !DivisionId.HasValue;

    public bool GrantsAllMediumStagesInMedium() =>
        EducationMediumId.HasValue
        && !EducationMediumStageId.HasValue
        && !StandardId.HasValue
        && !DivisionId.HasValue;

    public bool GrantsAllStandardsInMediumStage() =>
        EducationMediumStageId.HasValue
        && !StandardId.HasValue
        && !DivisionId.HasValue;

    public bool GrantsAllDivisionsInStandard() =>
        StandardId.HasValue
        && !DivisionId.HasValue;
}
