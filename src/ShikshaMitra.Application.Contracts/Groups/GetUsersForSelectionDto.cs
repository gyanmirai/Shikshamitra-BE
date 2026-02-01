using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Groups;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Application.Contracts.Groups;

/// <summary>
/// DTO for requesting users for name-based group selection.
/// Returns both students and employees in a combined result.
/// </summary>
public class GetUsersForSelectionDto : PagedAndSortedResultRequestDto, IValidatableObject
{
    /// <summary>
    /// Academic Year ID for context
    /// </summary>
    [Required]
    public Guid AcademicYearId { get; set; }

    /// <summary>
    /// Search term for filtering by name, roll no, employee code, etc.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Optional Division IDs filter for filtering students by division
    /// </summary>
    public List<Guid> DivisionFilters { get; set; } = new();

    /// <summary>
    /// Optional role filters (for employees)
    /// </summary>
    public List<Guid> RoleFilters { get; set; } = new();

    /// <summary>
    /// Additional student attribute filters
    /// </summary>
    public List<UserSelectionFilterDto> StudentFilters { get; set; } = new();

    /// <summary>
    /// Additional employee attribute filters
    /// </summary>
    public List<UserSelectionFilterDto> EmployeeFilters { get; set; } = new();

    public new IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var filter in StudentFilters)
        {
            if (!Enum.IsDefined(typeof(StudentRuleField), filter.Field))
            {
                yield return new ValidationResult(
                    $"Invalid filter field for students: {filter.Field}",
                    new[] { nameof(StudentFilters) });
            }

            if (!Enum.IsDefined(typeof(RuleOperator), filter.Operator))
            {
                yield return new ValidationResult(
                    $"Invalid filter operator: {filter.Operator}",
                    new[] { nameof(StudentFilters) });
            }
        }

        foreach (var filter in EmployeeFilters)
        {
            if (!Enum.IsDefined(typeof(EmployeeRuleField), filter.Field))
            {
                yield return new ValidationResult(
                    $"Invalid filter field for employees: {filter.Field}",
                    new[] { nameof(EmployeeFilters) });
            }

            if (!Enum.IsDefined(typeof(RuleOperator), filter.Operator))
            {
                yield return new ValidationResult(
                    $"Invalid filter operator: {filter.Operator}",
                    new[] { nameof(EmployeeFilters) });
            }
        }
    }
}

/// <summary>
/// DTO for a filter in user selection
/// </summary>
public class UserSelectionFilterDto
{
    /// <summary>
    /// Field to filter on (as integer matching StudentRuleField or EmployeeRuleField)
    /// </summary>
    public int Field { get; set; }

    /// <summary>
    /// Operator to apply
    /// </summary>
    public RuleOperator Operator { get; set; }

    /// <summary>
    /// Value to compare against
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// DTO for a user in selection results
/// </summary>
public class UserSelectionDto
{
    /// <summary>
    /// Member type (Student or Employee)
    /// </summary>
    public GroupMemberType MemberType { get; set; }

    /// <summary>
    /// ID of the student or employee entity
    /// </summary>
    public Guid MemberId { get; set; }

    /// <summary>
    /// User ID associated with the member
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Display name of the member
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Additional identifier (Roll No for students, Employee Code for employees)
    /// </summary>
    public string? Identifier { get; set; }

    /// <summary>
    /// Standard/Class name (for students)
    /// </summary>
    public string? StandardName { get; set; }

    /// <summary>
    /// Division/Section name (for students)
    /// </summary>
    public string? DivisionName { get; set; }

    /// <summary>
    /// Department (for employees)
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Role name (for employees)
    /// </summary>
    public string? RoleName { get; set; }
}
