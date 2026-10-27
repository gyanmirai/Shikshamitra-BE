using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Students.ValueObjects;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

internal static class StudentPortalOwnerSelection
{
	internal static IReadOnlyList<string> Relationships(StudentFamilyDetail? family)
	{
		return new[] { "father", "mother", "guardian" }.Where(relation => Present(Profile(family, relation))).ToArray();
	}

	internal static string? AutomaticRelationship(StudentFamilyDetail? family, string preferred)
	{
		// A saved guardian contact is never authority to create app access automatically.
		var allowed = Relationships(family).Where(relation => relation is "father" or "mother").ToArray();
		if (allowed.Contains(preferred)) return preferred;
		return allowed.FirstOrDefault(relation => !string.IsNullOrWhiteSpace(Profile(family, relation)?.Email))
			?? allowed.FirstOrDefault();
	}

	private static ParentGuardianDetails? Profile(StudentFamilyDetail? family, string relationship) => relationship switch
	{
		"father" => family?.FatherDetails,
		"mother" => family?.MotherDetails,
		_ => family?.GuardianDetails
	};

	private static bool Present(ParentGuardianDetails? person) => person != null &&
		(!string.IsNullOrWhiteSpace(person.FullName) || !string.IsNullOrWhiteSpace(person.Email) ||
		 !string.IsNullOrWhiteSpace(person.ContactNo));
}
