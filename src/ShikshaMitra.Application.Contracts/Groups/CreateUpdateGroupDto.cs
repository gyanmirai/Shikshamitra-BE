using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Groups;

namespace ShikshaMitra.Application.Contracts.Groups;

/// <summary>
/// DTO for creating or updating a Group.
/// Groups support two selection modes that can be used together:
/// - RuleBased: Automatically includes all users matching the criteria
/// - NameBased: Uses filters for context, includes only manually selected users
/// At least one selection mode must be provided.
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
    /// Rule-based selection configuration.
    /// All users matching the criteria are automatically included.
    /// </summary>
    public RuleBasedSelectionDto? RuleBased { get; set; }

    /// <summary>
    /// Name-based selection configuration.
    /// Uses filters for context, but only manually selected users are included.
    /// </summary>
    public NameBasedSelectionDto? NameBased { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Check if at least one selection mode is provided
        var hasRuleBased = RuleBased != null && RuleBased.HasAnySelection();
        var hasNameBased = NameBased != null && NameBased.SelectedMembers.Count > 0;

        if (!hasRuleBased && !hasNameBased)
        {
            yield return new ValidationResult(
                "Group must have at least one selection: RuleBased (with divisions, roles, or filters) or NameBased (with selected members).",
                new[] { nameof(RuleBased), nameof(NameBased) });
        }

        // Validate RuleBased selection if provided
        if (RuleBased != null)
        {
            foreach (var error in RuleBased.Validate("RuleBased"))
                yield return error;
        }

        // Validate NameBased selection if provided
        if (NameBased != null)
        {
            foreach (var error in NameBased.Validate("NameBased"))
                yield return error;
        }
    }
}

/// <summary>
/// Rule-based selection: All users matching the criteria are automatically included
/// </summary>
public class RuleBasedSelectionDto
{
    /// <summary>
    /// Division IDs for targeting students by division.
    /// All students in these divisions are automatically included.
    /// </summary>
    public List<Guid> DivisionIds { get; set; } = new();

    /// <summary>
    /// Role IDs for targeting employees by role.
    /// All employees with these roles are automatically included.
    /// </summary>
    public List<Guid> RoleIds { get; set; } = new();

    /// <summary>
    /// Student attribute filter rules
    /// </summary>
    public List<CreateStudentRuleDto> StudentFilters { get; set; } = new();

    /// <summary>
    /// Employee attribute filter rules
    /// </summary>
    public List<CreateEmployeeRuleDto> EmployeeFilters { get; set; } = new();

    public bool HasAnySelection()
    {
        return DivisionIds.Count > 0 ||
               RoleIds.Count > 0 ||
               StudentFilters.Count > 0 ||
               EmployeeFilters.Count > 0;
    }

    public IEnumerable<ValidationResult> Validate(string prefix)
    {
        foreach (var rule in StudentFilters)
        {
            if (!Enum.IsDefined(typeof(StudentRuleField), rule.Field))
            {
                yield return new ValidationResult(
                    $"Invalid student filter field in {prefix}: {rule.Field}",
                    new[] { $"{prefix}.StudentFilters" });
            }

            if (!Enum.IsDefined(typeof(RuleOperator), rule.Operator))
            {
                yield return new ValidationResult(
                    $"Invalid student filter operator in {prefix}: {rule.Operator}",
                    new[] { $"{prefix}.StudentFilters" });
            }

            if (string.IsNullOrWhiteSpace(rule.Value))
            {
                yield return new ValidationResult(
                    $"Student filter value cannot be empty in {prefix}.",
                    new[] { $"{prefix}.StudentFilters" });
            }
        }

        foreach (var rule in EmployeeFilters)
        {
            if (!Enum.IsDefined(typeof(EmployeeRuleField), rule.Field))
            {
                yield return new ValidationResult(
                    $"Invalid employee filter field in {prefix}: {rule.Field}",
                    new[] { $"{prefix}.EmployeeFilters" });
            }

            if (!Enum.IsDefined(typeof(RuleOperator), rule.Operator))
            {
                yield return new ValidationResult(
                    $"Invalid employee filter operator in {prefix}: {rule.Operator}",
                    new[] { $"{prefix}.EmployeeFilters" });
            }

            if (string.IsNullOrWhiteSpace(rule.Value))
            {
                yield return new ValidationResult(
                    $"Employee filter value cannot be empty in {prefix}.",
                    new[] { $"{prefix}.EmployeeFilters" });
            }
        }
    }
}

/// <summary>
/// Name-based selection: Uses filters for context, includes only manually selected users
/// </summary>
public class NameBasedSelectionDto : RuleBasedSelectionDto
{
    /// <summary>
    /// Manually selected members.
    /// Only these specific users are included in the group.
    /// </summary>
    public List<CreateGroupMemberDto> SelectedMembers { get; set; } = new();

    public new IEnumerable<ValidationResult> Validate(string prefix)
    {
        // Validate base filters
        foreach (var error in base.Validate(prefix))
            yield return error;

        // Validate selected members
        foreach (var member in SelectedMembers)
        {
            if (!Enum.IsDefined(typeof(GroupMemberType), member.MemberType))
            {
                yield return new ValidationResult(
                    $"Invalid member type in {prefix}: {member.MemberType}",
                    new[] { $"{prefix}.SelectedMembers" });
            }

            if (member.MemberId == Guid.Empty)
            {
                yield return new ValidationResult(
                    $"Member ID cannot be empty in {prefix}.",
                    new[] { $"{prefix}.SelectedMembers" });
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
