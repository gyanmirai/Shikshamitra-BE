using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionWorkflowConditionFieldRegistry
{
	private static readonly HashSet<string> ExactFields = new(StringComparer.OrdinalIgnoreCase)
	{
		"application.status"
	};

	private static readonly string[] ExtensiblePrefixes =
	[
		"application.",
		"inquiry.",
		"assessment.",
		"documents.",
		"payment.",
		"eligibility.",
		"approval.",
		"offer.",
		"seat.",
		"enrollment.",
		"appointment."
	];

	public static bool IsSupported(string field)
	{
		var normalized = field.Trim();
		if (ExactFields.Contains(normalized))
		{
			return true;
		}

		foreach (var prefix in ExtensiblePrefixes)
		{
			if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
				normalized.Length > prefix.Length)
			{
				return true;
			}
		}

		return false;
	}
}
