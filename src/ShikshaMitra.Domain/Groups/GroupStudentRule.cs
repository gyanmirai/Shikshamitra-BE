using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Groups;

/// <summary>
/// Represents a student attribute filter rule for rule-based groups
/// </summary>
public class GroupStudentRule : Entity<Guid>
{
    /// <summary>
    /// Parent group ID
    /// </summary>
    public Guid GroupId { get; private set; }

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

    protected GroupStudentRule() { }

    public GroupStudentRule(
        Guid id,
        Guid groupId,
        StudentRuleField field,
        RuleOperator ruleOperator,
        string value)
        : base(id)
    {
        GroupId = groupId;
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
