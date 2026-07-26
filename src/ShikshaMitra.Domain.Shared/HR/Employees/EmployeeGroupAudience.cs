using System;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees;

public static class EmployeeGroupAudience
{
	public const string ExtraPropertyName = "employeeGroupAudience";
	public const string Teaching = "Teaching";
	public const string NonTeaching = "NonTeaching";
	public const string Both = "Both";

	public static string ResolveOrDefault(string? audience)
	{
		if (string.IsNullOrWhiteSpace(audience))
		{
			return Both;
		}

		var normalizedAudience = audience.Trim();
		if (string.Equals(normalizedAudience, Teaching, StringComparison.OrdinalIgnoreCase))
		{
			return Teaching;
		}

		if (string.Equals(normalizedAudience, NonTeaching, StringComparison.OrdinalIgnoreCase))
		{
			return NonTeaching;
		}

		if (string.Equals(normalizedAudience, Both, StringComparison.OrdinalIgnoreCase))
		{
			return Both;
		}

		return normalizedAudience;
	}

	public static bool IsAssignableTo(string? audience, EmployeeGroupType? employeeGroup)
	{
		var resolvedAudience = ResolveOrDefault(audience);
		if (!employeeGroup.HasValue)
		{
			return string.Equals(resolvedAudience, Both, StringComparison.Ordinal);
		}

		if (string.Equals(resolvedAudience, Both, StringComparison.Ordinal))
		{
			return true;
		}

		return employeeGroup.Value switch
		{
			EmployeeGroupType.Teaching =>
				string.Equals(resolvedAudience, Teaching, StringComparison.Ordinal),
			EmployeeGroupType.NonTeaching =>
				string.Equals(resolvedAudience, NonTeaching, StringComparison.Ordinal),
			_ => false
		};
	}
}
