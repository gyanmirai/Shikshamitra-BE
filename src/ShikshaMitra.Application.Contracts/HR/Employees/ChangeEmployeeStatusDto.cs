using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// DTO for changing an employee's status.
/// </summary>
public class ChangeEmployeeStatusDto
{
	/// <summary>
	/// The new status to assign to the employee.
	/// </summary>
	public EmployeeStatusType EmployeeStatusType { get; set; }

	/// <summary>
	/// Optional reason for the change; required by domain rules when moving to Left or Retired.
	/// </summary>
	public string? Remarks { get; set; }
}
