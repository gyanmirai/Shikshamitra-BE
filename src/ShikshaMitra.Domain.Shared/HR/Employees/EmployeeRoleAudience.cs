using System;

namespace ShikshaMitra.HR.Employees;

public static class EmployeeRoleAudience
{
	public const string ExtraPropertyName = "roleAudience";
	public const string Employee = "Employee";
	public const string Student = "Student";
	public const string Parent = "Parent";

	public static string ForRoleName(string? roleName)
	{
		var normalizedName = roleName?.Trim();
		if (string.Equals(normalizedName, Student, StringComparison.OrdinalIgnoreCase))
		{
			return Student;
		}

		if (string.Equals(normalizedName, Parent, StringComparison.OrdinalIgnoreCase))
		{
			return Parent;
		}

		return Employee;
	}

	public static bool IsEmployeeAssignable(string? audience, string? roleName)
	{
		if (!string.IsNullOrWhiteSpace(audience))
		{
			return string.Equals(audience.Trim(), Employee, StringComparison.OrdinalIgnoreCase);
		}

		return string.Equals(ForRoleName(roleName), Employee, StringComparison.OrdinalIgnoreCase);
	}
}
