using System;
using System.Collections.Generic;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.Students;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.Groups;

/// <summary>
/// Data Transfer Object for reading Group data.
/// Groups support two selection modes that can be used together:
/// - RuleBased: Automatically includes all users matching the criteria
/// - NameBased: Uses filters for context, includes only manually selected users
/// </summary>
public class GroupDto : AuditedEntityDto<Guid>
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// Academic Year ID for filtering groups by academic year
    /// </summary>
    public Guid AcademicYearId { get; set; }

    /// <summary>
    /// Name of the group
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the group
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Rule-based selection configuration.
    /// All users matching the criteria are automatically included.
    /// </summary>
    public RuleBasedSelectionResponseDto? RuleBased { get; set; }

    /// <summary>
    /// Name-based selection configuration.
    /// Uses filters for context, but only manually selected users are included.
    /// </summary>
    public NameBasedSelectionResponseDto? NameBased { get; set; }

    /// <summary>
    /// Computed member count (resolved from rules and/or stored members)
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// Total count of student members in this group
    /// </summary>
    public int StudentCount { get; set; }

    /// <summary>
    /// Total count of employee members in this group
    /// </summary>
    public int EmployeeCount { get; set; }
}

/// <summary>
/// Academic selection containing unique IDs at each hierarchy level
/// </summary>
public class AcademicSelectionDto
{
    /// <summary>
    /// Unique Board IDs derived from selected divisions
    /// </summary>
    public List<Guid> Boards { get; set; } = new();

    /// <summary>
    /// Unique Medium IDs derived from selected divisions
    /// </summary>
    public List<Guid> Mediums { get; set; } = new();

    /// <summary>
    /// Unique Stage IDs derived from selected divisions
    /// </summary>
    public List<Guid> Stages { get; set; } = new();

    /// <summary>
    /// Unique Class/Standard IDs derived from selected divisions
    /// </summary>
    public List<Guid> Classes { get; set; } = new();

    /// <summary>
    /// Selected Division IDs
    /// </summary>
    public List<Guid> Divisions { get; set; } = new();
}

/// <summary>
/// Lightweight DTO containing role ID and name
/// </summary>
public class RoleInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Rule-based selection response DTO
/// </summary>
public class RuleBasedSelectionResponseDto
{
    /// <summary>
    /// Academic selection with full hierarchy IDs (Boards, Mediums, Stages, Classes, Divisions)
    /// </summary>
    public AcademicSelectionDto? AcademicSelection { get; set; }

    /// <summary>
    /// Roles with ID and Name for targeting employees by role
    /// </summary>
    public List<RoleInfoDto> Roles { get; set; } = new();

    /// <summary>
    /// Student attribute filter rules
    /// </summary>
    public List<StudentRuleDto> StudentFilters { get; set; } = new();

    /// <summary>
    /// Employee attribute filter rules
    /// </summary>
    public List<EmployeeRuleDto> EmployeeFilters { get; set; } = new();

    public bool HasAnySelection()
    {
        return (AcademicSelection != null && AcademicSelection.Divisions.Count > 0) ||
               Roles.Count > 0 ||
               StudentFilters.Count > 0 ||
               EmployeeFilters.Count > 0;
    }
}

/// <summary>
/// Name-based selection response DTO
/// </summary>
public class NameBasedSelectionResponseDto : RuleBasedSelectionResponseDto
{
    /// <summary>
    /// Manually selected members (flat list for backward compatibility)
    /// </summary>
    public List<GroupMemberDto> SelectedMembers { get; set; } = new();

    /// <summary>
    /// Student members with detailed info
    /// </summary>
    public List<GroupMemberStudentDto> Students { get; set; } = new();

    /// <summary>
    /// Employee members with detailed info
    /// </summary>
    public List<GroupMemberEmployeeDto> Employees { get; set; } = new();

    /// <summary>
    /// Total count of student members
    /// </summary>
    public int StudentCount { get; set; }

    /// <summary>
    /// Total count of employee members
    /// </summary>
    public int EmployeeCount { get; set; }
}

/// <summary>
/// DTO for student attribute filter rules
/// </summary>
public class StudentRuleDto
{
    public Guid? Id { get; set; }
    public StudentRuleField Field { get; set; }
    public RuleOperator Operator { get; set; }
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// DTO for employee attribute filter rules
/// </summary>
public class EmployeeRuleDto
{
    public Guid? Id { get; set; }
    public EmployeeRuleField Field { get; set; }
    public RuleOperator Operator { get; set; }
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// DTO for group members (name-based selection)
/// </summary>
public class GroupMemberDto
{
    public Guid? Id { get; set; }
    public GroupMemberType MemberType { get; set; }
    public Guid MemberId { get; set; }
    public Guid? UserId { get; set; }

    /// <summary>
    /// Display name of the member (populated from Student/Employee entity)
    /// </summary>
    public string? DisplayName { get; set; }
}

/// <summary>
/// Lightweight DTO for student group members
/// </summary>
public class GroupMemberStudentDto
{
    public Guid Id { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? StudentName { get; set; }
    public Guid? StandardId { get; set; }
    public string? StandardName { get; set; }
    public Guid? DivisionId { get; set; }
    public string? DivisionName { get; set; }
    public Guid? AcademicBoardId { get; set; }
    public string? AcademicBoardName { get; set; }
    public Guid AcademicMediumId { get; set; }
    public string? AcademicMediumName { get; set; }
    public GenderEnum? Gender { get; set; }
    public int? GrNo { get; set; }
    public int? RollNo { get; set; }
    public StudentLifeCycleStatusEnum Status { get; set; }
    public string? PrimaryContactNo { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// Lightweight DTO for employee group members
/// </summary>
public class GroupMemberEmployeeDto
{
    public Guid Id { get; set; }
    public string? EmployeeCode { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? EmployeeName { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public Guid RoleId { get; set; }
    public string? RoleName { get; set; }
    public GenderType? GenderType { get; set; }
    public string? MobileNo { get; set; }
    public string? Email { get; set; }
    public EmployeeStatusType? EmployeeStatusType { get; set; }
    public Guid? UserId { get; set; }
}
