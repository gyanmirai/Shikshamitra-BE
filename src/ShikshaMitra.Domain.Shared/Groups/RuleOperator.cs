namespace ShikshaMitra.Groups;

/// <summary>
/// Operators available for rule filtering
/// </summary>
public enum RuleOperator
{
    /// <summary>
    /// Equal to value
    /// </summary>
    Equals = 0,

    /// <summary>
    /// Not equal to value
    /// </summary>
    NotEquals = 1,

    /// <summary>
    /// Contains value (string)
    /// </summary>
    Contains = 2,

    /// <summary>
    /// Starts with value (string)
    /// </summary>
    StartsWith = 3,

    /// <summary>
    /// Ends with value (string)
    /// </summary>
    EndsWith = 4,

    /// <summary>
    /// Greater than value (numeric/date)
    /// </summary>
    GreaterThan = 5,

    /// <summary>
    /// Less than value (numeric/date)
    /// </summary>
    LessThan = 6,

    /// <summary>
    /// Greater than or equal to value (numeric/date)
    /// </summary>
    GreaterOrEqual = 7,

    /// <summary>
    /// Less than or equal to value (numeric/date)
    /// </summary>
    LessOrEqual = 8,

    /// <summary>
    /// Value is in a list (comma-separated)
    /// </summary>
    In = 9
}
