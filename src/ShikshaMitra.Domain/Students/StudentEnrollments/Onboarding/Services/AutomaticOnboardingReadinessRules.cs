using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Common admission-derived checks and the school's saved portal access choices.</summary>
public static class AutomaticOnboardingReadinessRules
{
	public const string SnapshotVersionKey = "AutomaticRequirementsVersion";
	public const int SnapshotVersion = 1;

	public static bool CanAdopt(StudentOnboardingCase item) => !item.StudentEnrollmentId.HasValue &&
		item.Status is not (StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled);

	public static bool HasCompleteIdentity(Student student) =>
		StudentCoreIdentityRules.IsComplete(student.FirstName, student.AdditionalDetail?.DateOfBirth?.Date,
			student.AdditionalDetail?.Gender);

	public static JsonElement Resolve(OnboardingRuleConfiguration? configuration)
	{
		var saved = configuration?.Root.GetProperty("readinessAccess");
		var rules = JsonNode.Parse(StudentPortalAccountRules.ForStaffSelection(saved).GetRawText())!.AsObject();
		rules["requireDocuments"] = true;
		rules["approvalMode"] = "none";
		rules["approvalTriggers"] = new JsonArray();
		using var document = JsonDocument.Parse(rules.ToJsonString());
		return document.RootElement.Clone();
	}
}
