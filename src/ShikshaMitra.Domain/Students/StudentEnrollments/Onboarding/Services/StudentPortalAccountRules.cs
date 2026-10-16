using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Separates permission to prepare an app account from the onboarding completion gate.</summary>
public static class StudentPortalAccountRules
{
	public const string NotNeeded = "not-needed";
	public const string Optional = "optional";
	public const string Required = "required";

	public static string Access(JsonElement rules, string accountType)
	{
		var key = accountType == "student" ? "studentAccount" : "guardianAccount";
		var access = OnboardingRuleConfiguration.Text(rules, key + "Access");
		// Old saved documents retain their exact account policy until staff explicitly changes it.
		if (access.Length == 0) return OnboardingRuleConfiguration.Boolean(rules, key) ? Required : NotNeeded;
		return access is Optional or Required ? access : NotNeeded;
	}

	public static bool IsEnabled(JsonElement rules, string accountType) => Access(rules, accountType) != NotNeeded;
	public static bool IsRequired(JsonElement rules, string accountType) => Access(rules, accountType) == Required;
}
