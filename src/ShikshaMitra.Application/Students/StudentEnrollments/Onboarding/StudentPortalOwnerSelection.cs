using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Students.ValueObjects;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

internal static class StudentPortalOwnerSelection
{
	internal static IReadOnlyList<string> Relationships(StudentFamilyDetail? family)
	{
		var parents = new[] { "father", "mother" }.Where(relation => Present(Profile(family, relation))).ToArray();
		return parents.Length > 0 ? parents : Present(family?.GuardianDetails) ? ["guardian"] : [];
	}

	internal static string? AutomaticRelationship(StudentFamilyDetail? family, string preferred)
	{
		var allowed = Relationships(family);
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
