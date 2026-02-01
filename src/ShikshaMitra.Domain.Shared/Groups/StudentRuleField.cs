namespace ShikshaMitra.Groups;

/// <summary>
/// Fields available for student rule filtering
/// </summary>
public enum StudentRuleField
{
    /// <summary>
    /// Filter by class/standard
    /// </summary>
    Class = 0,

    /// <summary>
    /// Filter by section/division
    /// </summary>
    Section = 1,

    /// <summary>
    /// Filter by gender
    /// </summary>
    Gender = 2,

    /// <summary>
    /// Filter by admission status (lifecycle status)
    /// </summary>
    AdmissionStatus = 3,

    /// <summary>
    /// Filter by student name
    /// </summary>
    Name = 4
}
