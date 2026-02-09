namespace ShikshaMitra.Groups;

/// <summary>
/// Fields available for employee rule filtering
/// </summary>
public enum EmployeeRuleField
{
    /// <summary>
    /// Filter by department
    /// </summary>
    Department = 0,

    /// <summary>
    /// Filter by role
    /// </summary>
    Role = 1,

    /// <summary>
    /// Filter by employment type (Teaching/NonTeaching)
    /// </summary>
    EmploymentType = 2,

    /// <summary>
    /// Filter by employee name
    /// </summary>
    Name = 3,

    /// <summary>
    /// Filter by gender
    /// </summary>
    Gender = 4
}
