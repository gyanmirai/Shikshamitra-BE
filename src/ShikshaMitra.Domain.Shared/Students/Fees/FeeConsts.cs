namespace ShikshaMitra.Students.Fees;

/// <summary>
/// Constants for the Student Fees sub-domain.
/// Mirrors <see cref="ShikshaMitra.HR.Payroll"/> pattern.
/// </summary>
public static class FeeConsts
{
    /// <summary>Maximum length for fee remarks.</summary>
    public const int MaxRemarksLength = 1000;

    /// <summary>Maximum length for concession reason.</summary>
    public const int MaxConcessionReasonLength = 500;

    /// <summary>Table name prefix for fee entities.</summary>
    public const string TablePrefix = "Sm";

    /// <summary>DB schema for fee tables.</summary>
    public const string? DbSchema = null;
}
