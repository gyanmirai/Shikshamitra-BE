using System;
using System.Collections.Generic;
using ShikshaMitra.HR.Employees;

namespace ShikshaMitra.Seeders;

internal static class RoleEmployeeAudienceDefaults
{
	private static readonly HashSet<string> EmployeeOnboardingRoles =
		new(StringComparer.OrdinalIgnoreCase)
		{
			"Director",
			"Principal",
			"Teacher",
			"Clerk",
			RolesSeederContributor.HodRoleName,
			"Vice Principal",
			"Administrative Manager",
			"HR Manager",
			"Finance Officer",
			"IT Administrator",
			"Office Assistant",
			"Admissions Receptionist",
			"Admissions Counselor",
			"Admissions Approver"
		};

	private static readonly Dictionary<string, string> GroupAudiences =
		new(StringComparer.OrdinalIgnoreCase)
		{
			{ RolesSeederContributor.AdminRoleName, EmployeeGroupAudience.NonTeaching },
			{ RolesSeederContributor.SuperAdminRoleName, EmployeeGroupAudience.NonTeaching },
			{ "Director", EmployeeGroupAudience.NonTeaching },
			{ "Principal", EmployeeGroupAudience.Teaching },
			{ "Teacher", EmployeeGroupAudience.Teaching },
			{ "Clerk", EmployeeGroupAudience.NonTeaching },
			{ "Student", EmployeeGroupAudience.Both },
			{ "Parent", EmployeeGroupAudience.Both },
			{ RolesSeederContributor.HodRoleName, EmployeeGroupAudience.Teaching },
			{ "Vice Principal", EmployeeGroupAudience.Teaching },
			{ "Administrative Manager", EmployeeGroupAudience.NonTeaching },
			{ "HR Manager", EmployeeGroupAudience.NonTeaching },
			{ "Finance Officer", EmployeeGroupAudience.NonTeaching },
			{ "IT Administrator", EmployeeGroupAudience.NonTeaching },
			{ "Office Assistant", EmployeeGroupAudience.NonTeaching },
			{ "Admissions Receptionist", EmployeeGroupAudience.NonTeaching },
			{ "Admissions Counselor", EmployeeGroupAudience.NonTeaching },
			{ "Admissions Approver", EmployeeGroupAudience.NonTeaching }
		};

	public static string ResolveRoleAudience(
		string roleName,
		string? existingAudience) =>
		string.IsNullOrWhiteSpace(existingAudience)
			? EmployeeRoleAudience.ForRoleName(roleName)
			: existingAudience;

	public static string ResolveEmployeeGroupAudience(
		string roleName,
		string? existingAudience)
	{
		if (!string.IsNullOrWhiteSpace(existingAudience))
		{
			return existingAudience;
		}

		return GroupAudiences.GetValueOrDefault(
			roleName,
			EmployeeGroupAudience.Both);
	}

	public static bool ResolveEmployeeOnboardingAssignable(
		string roleName,
		bool? existingValue)
	{
		if (EmployeeOnboardingRoleAssignment.IsReservedSystemRole(roleName) ||
			string.Equals(roleName, "Student", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(roleName, "Parent", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		return existingValue ?? EmployeeOnboardingRoles.Contains(roleName);
	}
}
