using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPlaybookApplicability
{
	public static void Normalize(AdmissionCrmPlaybookApplicabilityDto value)
	{
		value.Campus = Clean(value.Campus); value.DesiredClass = Clean(value.DesiredClass); value.Board = Clean(value.Board);
		value.Medium = Clean(value.Medium); value.ApplicantCategory = Clean(value.ApplicantCategory);
		value.AdmissionRound = Clean(value.AdmissionRound); value.AdmissionRoute = Clean(value.AdmissionRoute);
	}

	public static int Specificity(AdmissionCrmPlaybookApplicabilityDto value) => Values(value).Count(x => !string.IsNullOrWhiteSpace(x));

	public static bool Matches(AdmissionCrmPlaybookApplicabilityDto configured, AdmissionCrmPlaybookApplicabilityDto requested) =>
		Values(configured).Zip(Values(requested)).All(x => string.IsNullOrWhiteSpace(x.First) || Same(x.First, x.Second));

	public static bool Overlaps(AdmissionCrmPlaybookApplicabilityDto left, AdmissionCrmPlaybookApplicabilityDto right) =>
		Values(left).Zip(Values(right)).All(x => string.IsNullOrWhiteSpace(x.First) || string.IsNullOrWhiteSpace(x.Second) || Same(x.First, x.Second));

	private static IReadOnlyList<string?> Values(AdmissionCrmPlaybookApplicabilityDto value) =>
		[value.Campus, value.DesiredClass, value.Board, value.Medium, value.ApplicantCategory, value.AdmissionRound, value.AdmissionRoute];
	private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
