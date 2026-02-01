namespace ShikshaMitra.Groups;

/// <summary>
/// Defines how group members are selected
/// </summary>
public enum GroupSelectionMode
{
    /// <summary>
    /// Members are selected based on academic hierarchy, roles, and attribute rules
    /// </summary>
    RuleBased = 0,

    /// <summary>
    /// Members are manually selected by name/search
    /// </summary>
    NameBased = 1
}
