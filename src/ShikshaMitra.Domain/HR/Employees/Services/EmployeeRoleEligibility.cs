using System;
using Volo.Abp.Data;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.Employees;

public static class EmployeeRoleEligibility
{
	public static bool IsOnboardingAssignable(IdentityRole role) =>
		!string.IsNullOrWhiteSpace(role.Name) &&
		!EmployeeOnboardingRoleAssignment.IsReservedSystemRole(role.Name) &&
		!string.Equals(role.Name.Trim(), EmployeeRoleAudience.Student, StringComparison.OrdinalIgnoreCase) &&
		!string.Equals(role.Name.Trim(), EmployeeRoleAudience.Parent, StringComparison.OrdinalIgnoreCase) &&
		string.Equals(role.GetProperty<string>(EmployeeRoleAudience.ExtraPropertyName), EmployeeRoleAudience.Employee, StringComparison.Ordinal) &&
		role.GetProperty<bool>(EmployeeOnboardingRoleAssignment.ExtraPropertyName);
}
