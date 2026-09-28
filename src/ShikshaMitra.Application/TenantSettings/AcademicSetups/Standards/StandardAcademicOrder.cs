using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.TenantSettings.Standards;

internal static class StandardAcademicOrder
{
	internal static List<T> Sort<T>(IEnumerable<T> items, Func<T, string> name, Func<T, int> ageLowerLimit, Func<T, Guid> id) => items
		.OrderBy(item => GetRank(name(item)))
		.ThenBy(ageLowerLimit)
		.ThenBy(name, StringComparer.OrdinalIgnoreCase)
		.ThenBy(id)
		.ToList();

	internal static int GetRank(string name)
	{
		var normalized = name.Trim();
		if (normalized.Equals("Nursery", StringComparison.OrdinalIgnoreCase)) return 0;
		if (normalized.Equals("LKG", StringComparison.OrdinalIgnoreCase)) return 1;
		if (normalized.Equals("UKG", StringComparison.OrdinalIgnoreCase)) return 2;

		if (normalized.StartsWith("Class ", StringComparison.OrdinalIgnoreCase) &&
			int.TryParse(normalized.AsSpan("Class ".Length).Trim(), out var grade) &&
			grade > 0 && grade < int.MaxValue - 2)
		{
			return grade + 2;
		}

		return int.MaxValue;
	}
}
