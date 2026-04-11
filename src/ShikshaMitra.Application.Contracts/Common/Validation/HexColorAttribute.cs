using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ShikshaMitra.Common.Validation;

/// <summary>
/// Validates that a string is a valid hex color code (#RRGGBB or #RGB format)
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class HexColorAttribute : ValidationAttribute
{
    private static readonly Regex HexColorRegex = new Regex(
        @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public override string FormatErrorMessage(string name)
    {
        return $"The field {name} must be a valid hex color in the format #RRGGBB or #RGB";
    }

    public override bool IsValid(object? value)
    {
        if (value == null)
            return true; // Allow null values; use [Required] separately if needed

        if (value is not string stringValue)
            return false;

        if (string.IsNullOrWhiteSpace(stringValue))
            return false;

        return HexColorRegex.IsMatch(stringValue);
    }
}
