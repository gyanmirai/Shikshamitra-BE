using System;
using System.Collections.Generic;
using System.Linq;
using static ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegisterProjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

// Explicit allowlist: never execute a client-supplied dynamic expression.
internal static class AttendanceRegisterOrdering
{
	internal static List<GroupedRegisterProjection> Apply(List<GroupedRegisterProjection> rows, string? sorting)
	{
		if (string.IsNullOrWhiteSpace(sorting)) return rows;
		var parts = sorting.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
		Func<GroupedRegisterProjection, object?>? key = parts[0] switch
		{
			"employeeName" => row => row.EmployeeName,
			"present" => row => row.Present,
			"employeeGroup" => row => row.EmployeeGroup,
			"department" => row => row.Department,
			_ => null
		};
		if (key == null) return rows;
		var ordered = descending ? rows.OrderByDescending(key) : rows.OrderBy(key);
		return ordered.ThenBy(row => row.EmployeeId).ToList();
	}
}
