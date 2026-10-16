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
		!string.IsNullOrWhiteSpace(student.FirstName) && !string.IsNullOrWhiteSpace(student.LastName) &&
		student.AdditionalDetail?.DateOfBirth.HasValue == true && student.AdditionalDetail.Gender.HasValue;

	public static JsonElement Resolve(OnboardingRuleConfiguration? configuration)
	{
		var saved = configuration?.Root.GetProperty("readinessAccess");
		var rules = saved.HasValue ? JsonNode.Parse(saved.Value.GetRawText())!.AsObject() : new JsonObject();
		// Account preparation and credential delivery are independent choices. Existing delivery
		// settings are preserved; requiring an account never enables an outbound email implicitly.
		rules["studentAccount"] = saved.HasValue && StudentPortalAccountRules.IsRequired(saved.Value, "student");
		rules["guardianAccount"] = !saved.HasValue || (saved.Value.TryGetProperty("guardianAccountAccess", out _)
			? StudentPortalAccountRules.IsRequired(saved.Value, "guardian") : OnboardingRuleConfiguration.Boolean(saved.Value, "guardianAccount", true));
		var accountRules = JsonSerializer.SerializeToElement(rules);
		rules["sendStudentCredentialEmail"] = saved.HasValue && StudentPortalAccountRules.IsEnabled(accountRules, "student") &&
			OnboardingRuleConfiguration.Boolean(saved.Value, "sendStudentCredentialEmail");
		rules["sendGuardianCredentialEmail"] = saved.HasValue && StudentPortalAccountRules.IsEnabled(accountRules, "guardian") &&
			OnboardingRuleConfiguration.Boolean(saved.Value, "sendGuardianCredentialEmail");
		rules["requireDocuments"] = true;
		rules["approvalMode"] = "none";
		rules["approvalTriggers"] = new JsonArray();
		using var document = JsonDocument.Parse(rules.ToJsonString());
		return document.RootElement.Clone();
	}
}
