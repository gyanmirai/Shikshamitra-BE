using System;
using System.Collections.Generic;
using ShikshaMitra.Groups;
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
}

/// <summary>
/// Rule-based selection response DTO
/// </summary>
public class RuleBasedSelectionResponseDto
{
    /// <summary>
    /// Division IDs for targeting students by division
    /// </summary>
    public List<Guid> DivisionIds { get; set; } = new();

    /// <summary>
    /// Role IDs for targeting employees by role
    /// </summary>
    public List<Guid> RoleIds { get; set; } = new();

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
        return DivisionIds.Count > 0 ||
               RoleIds.Count > 0 ||
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
    /// Manually selected members
    /// </summary>
    public List<GroupMemberDto> SelectedMembers { get; set; } = new();
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
