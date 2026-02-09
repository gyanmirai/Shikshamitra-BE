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
    /// Display-friendly name
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Data type of the field (String, Enum)
    /// </summary>
    public string Type { get; set; } = default!;

    /// <summary>
    /// List of operator names applicable to this field
    /// </summary>
    public List<string> Operators { get; set; } = new();

    /// <summary>
    /// Selectable options for Enum fields; null for String fields
    /// </summary>
    public List<RuleFieldOptionDto>? Options { get; set; }
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
}
