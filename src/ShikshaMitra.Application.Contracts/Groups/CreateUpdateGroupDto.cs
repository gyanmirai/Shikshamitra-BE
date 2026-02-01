using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Groups;

namespace ShikshaMitra.Application.Contracts.Groups;

/// <summary>
/// DTO for creating or updating a Group
/// </summary>
public class CreateUpdateGroupDto : IValidatableObject
{
    /// <summary>
    /// Name of the group
    /// </summary>
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the group
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Selection mode: RuleBased or NameBased
    /// </summary>
    [Required]
    public GroupSelectionMode SelectionMode { get; set; }

    /// <summary>
    /// Selected Division IDs for targeting students by division
    /// Required for RuleBased groups (frontend resolves higher hierarchy to division IDs)
    /// </summary>
    public List<Guid> SelectedDivisionIds { get; set; } = new();

    /// <summary>
    /// Selected role IDs for targeting employees by role
    /// Optional for RuleBased groups
    /// </summary>
    public List<Guid> RoleIds { get; set; } = new();

    /// <summary>
    /// Student attribute filter rules
    /// Optional for RuleBased groups
    /// </summary>
    public List<CreateStudentRuleDto> StudentRules { get; set; } = new();

    /// <summary>
    /// Employee attribute filter rules
    /// Optional for RuleBased groups
    /// </summary>
    public List<CreateEmployeeRuleDto> EmployeeRules { get; set; } = new();

    /// <summary>
    /// Manually selected members
    /// Required for NameBased groups
    /// </summary>
    public List<CreateGroupMemberDto> Members { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SelectionMode == GroupSelectionMode.RuleBased)
        {
            // Rule-based groups must have at least one selection criteria
            var hasDivisionSelection = SelectedDivisionIds.Count > 0;
            var hasRoleSelection = RoleIds.Count > 0;
            var hasStudentRules = StudentRules.Count > 0;
            var hasEmployeeRules = EmployeeRules.Count > 0;

            if (!hasDivisionSelection && !hasRoleSelection && !hasStudentRules && !hasEmployeeRules)
            {
                yield return new ValidationResult(
                    "Rule-based groups must have at least one selection criteria (Division Selections, Roles, Student Rules, or Employee Rules).",
                    new[] { nameof(SelectedDivisionIds), nameof(RoleIds), nameof(StudentRules), nameof(EmployeeRules) });
            }

            // Validate student rules
            foreach (var rule in StudentRules)
            {
                if (!Enum.IsDefined(typeof(StudentRuleField), rule.Field))
                {
                    yield return new ValidationResult(
                        $"Invalid student rule field: {rule.Field}",
                        new[] { nameof(StudentRules) });
                }

                if (!Enum.IsDefined(typeof(RuleOperator), rule.Operator))
                {
                    yield return new ValidationResult(
                        $"Invalid student rule operator: {rule.Operator}",
                        new[] { nameof(StudentRules) });
                }

                if (string.IsNullOrWhiteSpace(rule.Value))
                {
                    yield return new ValidationResult(
                        "Student rule value cannot be empty.",
                        new[] { nameof(StudentRules) });
                }
            }

            // Validate employee rules
            foreach (var rule in EmployeeRules)
            {
                if (!Enum.IsDefined(typeof(EmployeeRuleField), rule.Field))
                {
                    yield return new ValidationResult(
                        $"Invalid employee rule field: {rule.Field}",
                        new[] { nameof(EmployeeRules) });
                }

                if (!Enum.IsDefined(typeof(RuleOperator), rule.Operator))
                {
                    yield return new ValidationResult(
                        $"Invalid employee rule operator: {rule.Operator}",
                        new[] { nameof(EmployeeRules) });
                }

                if (string.IsNullOrWhiteSpace(rule.Value))
                {
                    yield return new ValidationResult(
                        "Employee rule value cannot be empty.",
                        new[] { nameof(EmployeeRules) });
                }
            }
        }
        else if (SelectionMode == GroupSelectionMode.NameBased)
        {
            // Name-based groups must have at least one member
            if (Members.Count == 0)
            {
                yield return new ValidationResult(
                    "Name-based groups must have at least one member.",
                    new[] { nameof(Members) });
            }

            // Validate members
            foreach (var member in Members)
            {
                if (!Enum.IsDefined(typeof(GroupMemberType), member.MemberType))
                {
                    yield return new ValidationResult(
                        $"Invalid member type: {member.MemberType}",
                        new[] { nameof(Members) });
                }

                if (member.MemberId == Guid.Empty)
                {
                    yield return new ValidationResult(
                        "Member ID cannot be empty.",
                        new[] { nameof(Members) });
                }
            }
        }
    }
}

/// <summary>
/// DTO for creating a student rule
/// </summary>
public class CreateStudentRuleDto
{
    [Required]
    public StudentRuleField Field { get; set; }

    [Required]
    public RuleOperator Operator { get; set; }

    [Required]
    [StringLength(500)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating an employee rule
/// </summary>
public class CreateEmployeeRuleDto
{
    [Required]
    public EmployeeRuleField Field { get; set; }

    [Required]
    public RuleOperator Operator { get; set; }

    [Required]
    [StringLength(500)]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating a group member
/// </summary>
public class CreateGroupMemberDto
{
    [Required]
    public GroupMemberType MemberType { get; set; }

    [Required]
    public Guid MemberId { get; set; }

    /// <summary>
    /// Optional User ID (will be resolved from Student/Employee if not provided)
    /// </summary>
    public Guid? UserId { get; set; }
}
