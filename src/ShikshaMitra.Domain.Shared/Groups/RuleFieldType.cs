namespace ShikshaMitra.Groups;

/// <summary>
/// Data type of a rule field, determines which operators are applicable
/// </summary>
public enum RuleFieldType
{
    /// <summary>
    /// Free-text string field (supports Contains, StartsWith, EndsWith, etc.)
    /// </summary>
    String = 0,

    /// <summary>
    /// Enum/select field with predefined options (supports Equals, NotEquals, In)
    /// </summary>
    Enum = 1
}
