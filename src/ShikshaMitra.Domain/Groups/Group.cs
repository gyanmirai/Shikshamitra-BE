using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Groups;

/// <summary>
/// Represents a group for audience targeting in holidays and events.
/// Groups support two selection modes that can be used together:
/// - RuleBased: Automatically includes all users matching the criteria
/// - NameBased: Uses filters for context, includes only manually selected users
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

    #region Rule-Based Selection Properties

    /// <summary>
    /// Division IDs for rule-based selection - JSON serialized
    /// All students in these divisions are automatically included
    /// </summary>
    public List<Guid> RuleBasedDivisionIds { get; private set; } = new();

    /// <summary>
    /// Role IDs for rule-based selection - JSON serialized
    /// All employees with these roles are automatically included
    /// </summary>
    public List<Guid> RuleBasedRoleIds { get; private set; } = new();

    #endregion

    #region Name-Based Selection Properties

    /// <summary>
    /// Division IDs for name-based selection context - JSON serialized
    /// Used to filter students when manually selecting members
    /// </summary>
    public List<Guid> NameBasedDivisionIds { get; private set; } = new();

    /// <summary>
    /// Role IDs for name-based selection context - JSON serialized
    /// Used to filter employees when manually selecting members
    /// </summary>
    public List<Guid> NameBasedRoleIds { get; private set; } = new();

    #endregion

    /// <summary>
    /// Student attribute filter rules (for both RuleBased and NameBased selections)
    /// Use SelectionType property on each rule to distinguish
    /// </summary>
    public ICollection<GroupStudentRule> StudentRules { get; private set; } = new List<GroupStudentRule>();

    /// <summary>
    /// Employee attribute filter rules (for both RuleBased and NameBased selections)
    /// Use SelectionType property on each rule to distinguish
    /// </summary>
    public ICollection<GroupEmployeeRule> EmployeeRules { get; private set; } = new List<GroupEmployeeRule>();

    /// <summary>
    /// Manually selected members for name-based selection
    /// </summary>
    public ICollection<GroupMember> Members { get; private set; } = new List<GroupMember>();

    protected Group() { }

    internal Group(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        string name,
        string? description)
        : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        SetName(name);
        Description = description;
    }

    /// <summary>
    /// Factory method to create a new group
    /// </summary>
    public static Group Create(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        string name,
        string? description)
    {
        return new Group(id, tenantId, academicYearId, name, description);
    }

    /// <summary>
    /// Factory method to create a new group with selection configurations
    /// </summary>
    public static Group Create(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        string name,
        string? description,
        List<Guid> ruleBasedDivisionIds,
        List<Guid> ruleBasedRoleIds,
        List<Guid> nameBasedDivisionIds,
        List<Guid> nameBasedRoleIds)
    {
        var group = new Group(id, tenantId, academicYearId, name, description);
        group.SetRuleBasedDivisionIds(ruleBasedDivisionIds);
        group.SetRuleBasedRoleIds(ruleBasedRoleIds);
        group.SetNameBasedDivisionIds(nameBasedDivisionIds);
        group.SetNameBasedRoleIds(nameBasedRoleIds);
        return group;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 256);
    }

    public void SetDescription(string? description)
    {
        Description = description;
    }

    #region Rule-Based Selection Methods

    public void SetRuleBasedDivisionIds(List<Guid> divisionIds)
    {
        RuleBasedDivisionIds = divisionIds ?? new List<Guid>();
    }

    public void SetRuleBasedRoleIds(List<Guid> roleIds)
    {
        RuleBasedRoleIds = roleIds ?? new List<Guid>();
    }

    public IEnumerable<GroupStudentRule> GetRuleBasedStudentRules()
    {
        return StudentRules.Where(r => r.SelectionType == GroupSelectionType.RuleBased);
    }

    public IEnumerable<GroupEmployeeRule> GetRuleBasedEmployeeRules()
    {
        return EmployeeRules.Where(r => r.SelectionType == GroupSelectionType.RuleBased);
    }

    #endregion

    #region Name-Based Selection Methods

    public void SetNameBasedDivisionIds(List<Guid> divisionIds)
    {
        NameBasedDivisionIds = divisionIds ?? new List<Guid>();
    }

    public void SetNameBasedRoleIds(List<Guid> roleIds)
    {
        NameBasedRoleIds = roleIds ?? new List<Guid>();
    }

    public IEnumerable<GroupStudentRule> GetNameBasedStudentRules()
    {
        return StudentRules.Where(r => r.SelectionType == GroupSelectionType.NameBased);
    }

    public IEnumerable<GroupEmployeeRule> GetNameBasedEmployeeRules()
    {
        return EmployeeRules.Where(r => r.SelectionType == GroupSelectionType.NameBased);
    }

    #endregion

    public void AddStudentRule(GroupStudentRule rule)
    {
        StudentRules.Add(rule);
    }

    public void ClearStudentRules()
    {
        StudentRules.Clear();
    }

    public void ClearStudentRules(GroupSelectionType selectionType)
    {
        var rulesToRemove = StudentRules.Where(r => r.SelectionType == selectionType).ToList();
        foreach (var rule in rulesToRemove)
            StudentRules.Remove(rule);
    }

    public void AddEmployeeRule(GroupEmployeeRule rule)
    {
        EmployeeRules.Add(rule);
    }

    public void ClearEmployeeRules()
    {
        EmployeeRules.Clear();
    }

    public void ClearEmployeeRules(GroupSelectionType selectionType)
    {
        var rulesToRemove = EmployeeRules.Where(r => r.SelectionType == selectionType).ToList();
        foreach (var rule in rulesToRemove)
            EmployeeRules.Remove(rule);
    }

    public void AddMember(GroupMember member)
    {
        Members.Add(member);
    }

    public void ClearMembers()
    {
        Members.Clear();
    }

    public void Update(string name, string? description)
    {
        SetName(name);
        SetDescription(description);
    }

    /// <summary>
    /// Update rule-based selection configuration
    /// </summary>
    public void UpdateRuleBasedSelection(List<Guid> divisionIds, List<Guid> roleIds)
    {
        SetRuleBasedDivisionIds(divisionIds);
        SetRuleBasedRoleIds(roleIds);
    }

    /// <summary>
    /// Update name-based selection configuration
    /// </summary>
    public void UpdateNameBasedSelection(List<Guid> divisionIds, List<Guid> roleIds)
    {
        SetNameBasedDivisionIds(divisionIds);
        SetNameBasedRoleIds(roleIds);
    }
}
