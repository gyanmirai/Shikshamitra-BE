using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Groups;

/// <summary>
/// Represents a student attribute filter rule for groups
/// </summary>
public class GroupStudentRule : Entity<Guid>
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
    public StudentRuleField Field { get; private set; }

    /// <summary>
    /// The operator to apply
    /// </summary>
    public RuleOperator Operator { get; private set; }

    /// <summary>
    /// The value to compare against
    /// </summary>
    public string Value { get; private set; }

    protected GroupStudentRule()
    {
        // EF materializes these required columns after this ORM constructor.
        Value = null!;
    }

    public GroupStudentRule(
        Guid id,
        Guid groupId,
        GroupSelectionType selectionType,
        StudentRuleField field,
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

    public void Update(StudentRuleField field, RuleOperator ruleOperator, string value)
    {
        Field = field;
        Operator = ruleOperator;
        Value = value ?? string.Empty;
    }
}
