using System;
using System.Collections.Generic;
using ShikshaMitra.Groups;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.Groups;

/// <summary>
/// Data Transfer Object for reading Group data
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
    /// Selection mode: RuleBased or NameBased
    /// </summary>
    public GroupSelectionMode SelectionMode { get; set; }

    /// <summary>
    /// Selected Division IDs for targeting students by division
    /// Used when SelectionMode is RuleBased
    /// </summary>
    public List<Guid> SelectedDivisionIds { get; set; } = new();

    /// <summary>
    /// Selected role IDs for targeting employees by role
    /// Used when SelectionMode is RuleBased
    /// </summary>
    public List<Guid> RoleIds { get; set; } = new();

    /// <summary>
    /// Student attribute filter rules
    /// Used when SelectionMode is RuleBased
    /// </summary>
    public List<StudentRuleDto> StudentRules { get; set; } = new();

    /// <summary>
    /// Employee attribute filter rules
    /// Used when SelectionMode is RuleBased
    /// </summary>
    public List<EmployeeRuleDto> EmployeeRules { get; set; } = new();

    /// <summary>
    /// Manually selected members
    /// Used when SelectionMode is NameBased
    /// </summary>
    public List<GroupMemberDto> Members { get; set; } = new();

    /// <summary>
    /// Computed member count (resolved from rules or stored members)
    /// </summary>
    public int MemberCount { get; set; }
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
