using System;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionAssessmentFilterValues
{
	public static Guid[] Standards(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return [];
		var parts = value.Split(',', StringSplitOptions.TrimEntries);
		if (value.Length > 800 || parts.Length > 20 || parts.Any(part => !Guid.TryParse(part, out var id) || id == Guid.Empty))
			throw AdmissionAssessmentSchedulingService.Invalid("Choose up to 20 valid classes.");
		return parts.Select(Guid.Parse).Distinct().ToArray();
	}
}
