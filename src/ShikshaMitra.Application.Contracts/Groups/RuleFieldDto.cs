using System.Collections.Generic;

namespace ShikshaMitra.Application.Contracts.Groups;

/// <summary>
/// Describes a single rule field with its applicable operators and selectable options
/// </summary>
public class RuleFieldDto
{
    /// <summary>
    /// Field identifier (enum name, e.g. "Gender", "Department")
    /// </summary>
    public string Field { get; set; } = default!;

    /// <summary>
    /// Numeric enum value of the field (e.g. StudentRuleField.Gender = 2)
    /// </summary>
    public int FieldValue { get; set; }

    /// <summary>
    /// Display-friendly name
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Data type of the field (String, Enum)
    /// </summary>
    public string Type { get; set; } = default!;

    /// <summary>
    /// Numeric enum value of the field type (e.g. RuleFieldType.Enum = 1)
    /// </summary>
    public int TypeValue { get; set; }

    /// <summary>
    /// List of applicable operators with name and numeric value
    /// </summary>
    public List<RuleFieldOperatorDto> Operators { get; set; } = new();

    /// <summary>
    /// Selectable options for Enum fields; null for String fields
    /// </summary>
    public List<RuleFieldOptionDto>? Options { get; set; }
}

/// <summary>
/// Operator with name and numeric enum value
/// </summary>
public class RuleFieldOperatorDto
{
    /// <summary>
    /// Operator name (e.g. "Equals", "Contains")
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Numeric enum value (e.g. RuleOperator.Equals = 0)
    /// </summary>
    public int Value { get; set; }
}

/// <summary>
/// A selectable option for an Enum-type rule field
/// </summary>
public class RuleFieldOptionDto
{
    /// <summary>
    /// The enum value name (e.g. "Male", "Teaching")
    /// </summary>
    public string Value { get; set; } = default!;

    /// <summary>
    /// Display label (e.g. "Male", "Teaching")
    /// </summary>
    public string Label { get; set; } = default!;

    /// <summary>
    /// Numeric enum value (e.g. GenderEnum.Male = 0)
    /// </summary>
    public int IntValue { get; set; }
}
