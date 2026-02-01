using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Groups;

/// <summary>
/// Represents a group for audience targeting in holidays and events.
/// Groups can be rule-based (academic hierarchy + roles + attribute filters) or name-based (manual selection).
/// </summary>
public class Group : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    /// <summary>
    /// Academic Year ID for filtering groups by academic year
    /// </summary>
    public Guid AcademicYearId { get; private set; }

    /// <summary>
    /// Name of the group
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Optional description of the group
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Selection mode: RuleBased or NameBased
    /// </summary>
    public GroupSelectionMode SelectionMode { get; private set; }

    /// <summary>
    /// Selected Division IDs for targeting students by division - JSON serialized
    /// Used when SelectionMode is RuleBased
    /// </summary>
    public List<Guid> SelectedDivisionIds { get; private set; } = new();

    /// <summary>
    /// Selected role IDs for targeting employees by role - JSON serialized
    /// Used when SelectionMode is RuleBased
    /// </summary>
    public List<Guid> RoleIds { get; private set; } = new();

    /// <summary>
    /// Student attribute filter rules
    /// Used when SelectionMode is RuleBased
    /// </summary>
    public ICollection<GroupStudentRule> StudentRules { get; private set; } = new List<GroupStudentRule>();

    /// <summary>
    /// Employee attribute filter rules
    /// Used when SelectionMode is RuleBased
    /// </summary>
    public ICollection<GroupEmployeeRule> EmployeeRules { get; private set; } = new List<GroupEmployeeRule>();

    /// <summary>
    /// Manually selected members
    /// Used when SelectionMode is NameBased
    /// </summary>
    public ICollection<GroupMember> Members { get; private set; } = new List<GroupMember>();

    protected Group() { }

    internal Group(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        string name,
        string? description,
        GroupSelectionMode selectionMode)
        : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        SetName(name);
        Description = description;
        SelectionMode = selectionMode;
    }

    /// <summary>
    /// Factory method to create a new rule-based group
    /// </summary>
    public static Group CreateRuleBased(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        string name,
        string? description,
        List<Guid>? selectedDivisionIds = null,
        List<Guid>? roleIds = null)
    {
        var group = new Group(id, tenantId, academicYearId, name, description, GroupSelectionMode.RuleBased);

        if (selectedDivisionIds != null)
            group.SelectedDivisionIds = selectedDivisionIds;

        if (roleIds != null)
            group.RoleIds = roleIds;

        return group;
    }

    /// <summary>
    /// Factory method to create a new name-based group
    /// </summary>
    public static Group CreateNameBased(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        string name,
        string? description)
    {
        return new Group(id, tenantId, academicYearId, name, description, GroupSelectionMode.NameBased);
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 256);
    }

    public void SetDescription(string? description)
    {
        Description = description;
    }

    public void SetDivisionSelections(List<Guid> divisionIds)
    {
        if (SelectionMode != GroupSelectionMode.RuleBased)
            throw new InvalidOperationException("Division selections can only be set for rule-based groups.");

        SelectedDivisionIds = divisionIds ?? new List<Guid>();
    }

    public void SetRoleIds(List<Guid> roleIds)
    {
        if (SelectionMode != GroupSelectionMode.RuleBased)
            throw new InvalidOperationException("Role IDs can only be set for rule-based groups.");

        RoleIds = roleIds ?? new List<Guid>();
    }

    public void AddStudentRule(GroupStudentRule rule)
    {
        if (SelectionMode != GroupSelectionMode.RuleBased)
            throw new InvalidOperationException("Student rules can only be added to rule-based groups.");

        StudentRules.Add(rule);
    }

    public void ClearStudentRules()
    {
        StudentRules.Clear();
    }

    public void AddEmployeeRule(GroupEmployeeRule rule)
    {
        if (SelectionMode != GroupSelectionMode.RuleBased)
            throw new InvalidOperationException("Employee rules can only be added to rule-based groups.");

        EmployeeRules.Add(rule);
    }

    public void ClearEmployeeRules()
    {
        EmployeeRules.Clear();
    }

    public void AddMember(GroupMember member)
    {
        if (SelectionMode != GroupSelectionMode.NameBased)
            throw new InvalidOperationException("Members can only be added to name-based groups.");

        Members.Add(member);
    }

    public void ClearMembers()
    {
        Members.Clear();
    }

    public void Update(
        string name,
        string? description,
        List<Guid>? selectedDivisionIds = null,
        List<Guid>? roleIds = null)
    {
        SetName(name);
        SetDescription(description);

        if (SelectionMode == GroupSelectionMode.RuleBased)
        {
            if (selectedDivisionIds != null)
                SelectedDivisionIds = selectedDivisionIds;

            if (roleIds != null)
                RoleIds = roleIds;
        }
    }
}
