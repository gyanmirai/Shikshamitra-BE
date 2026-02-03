namespace ShikshaMitra.Groups;

/// <summary>
/// Defines which selection type a rule or setting belongs to
/// </summary>
public enum GroupSelectionType
{
    /// <summary>
    /// Rule-based selection: automatically includes all matching users
    /// </summary>
    RuleBased = 0,

    /// <summary>
    /// Name-based selection: uses filters for context, includes only manually selected users
    /// </summary>
    NameBased = 1
}
