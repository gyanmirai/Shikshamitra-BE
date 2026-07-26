using System;

namespace ShikshaMitra.HR.Employees;

public static class EmployeeOnboardingRoleAssignment
{
	public const string ExtraPropertyName = "employeeOnboardingAssignable";

	public static bool IsReservedSystemRole(string? roleName) =>
		string.Equals(roleName?.Trim(), "admin", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(roleName?.Trim(), "super-admin", StringComparison.OrdinalIgnoreCase);
}
