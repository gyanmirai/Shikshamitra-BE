using System;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public class EmployeeAssignableRoleDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string RoleAudience { get; set; } = EmployeeRoleAudience.Employee;
	public string EmployeeGroupAudience { get; set; } = global::ShikshaMitra.HR.Employees.EmployeeGroupAudience.Both;
	public bool EmployeeOnboardingAssignable { get; set; } = true;
}
