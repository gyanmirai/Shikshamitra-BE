using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Groups;

/// <summary>
/// Represents an employee attribute filter rule for groups
/// </summary>
public class GroupEmployeeRule : Entity<Guid>
{
    /// <summary>
    /// Parent group ID
    /// </summary>
    public Guid GroupId { get; private set; }

    /// <summary>
    /// Which selection type this rule belongs to (RuleBased or NameBased)
    /// </summary>
    public GroupSelectionType SelectionType { get; private set; }

    /// <summary>
    /// The field to filter on
    /// </summary>
    public EmployeeRuleField Field { get; private set; }

    /// <summary>
    /// The operator to apply
    /// </summary>
    public RuleOperator Operator { get; private set; }

    /// <summary>
    /// The value to compare against
    /// </summary>
    public string Value { get; private set; }

    protected GroupEmployeeRule() { }

    public GroupEmployeeRule(
        Guid id,
        Guid groupId,
        GroupSelectionType selectionType,
        EmployeeRuleField field,
        RuleOperator ruleOperator,
        string value)
        : base(id)
    {
        GroupId = groupId;
        SelectionType = selectionType;
        Field = field;
        Operator = ruleOperator;
        Value = value ?? string.Empty;
    }

    public void Update(EmployeeRuleField field, RuleOperator ruleOperator, string value)
    {
        Field = field;
        Operator = ruleOperator;
        Value = value ?? string.Empty;
    }
}
