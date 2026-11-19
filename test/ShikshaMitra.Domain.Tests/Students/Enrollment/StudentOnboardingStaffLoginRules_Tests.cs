using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentOnboardingStaffLoginRules_Tests
{
	[Theory]
	[InlineData("required")]
	[InlineData("not-needed")]
	public void Runtime_login_is_requested_by_staff_without_mutating_historical_rules(string access)
	{
		var saved = JsonSerializer.SerializeToElement(new { studentAccountAccess = access,
			guardianAccountAccess = access, studentAccount = true, guardianAccount = true,
			credentialProvisioningMode = "automatic", credentialSendingTime = "completion",
			sendStudentCredentialEmail = true, sendGuardianCredentialEmail = true, requireDocuments = true });
		var rules = StudentPortalAccountRules.ForStaffSelection(saved);
		StudentPortalAccountRules.Access(rules, "student").ShouldBe("optional");
		StudentPortalAccountRules.Access(rules, "guardian").ShouldBe("optional");
		OnboardingRuleConfiguration.Boolean(rules, "studentAccount").ShouldBeFalse();
		OnboardingRuleConfiguration.Boolean(rules, "guardianAccount").ShouldBeFalse();
		OnboardingRuleConfiguration.Text(rules, "credentialProvisioningMode").ShouldBe("manual");
		OnboardingRuleConfiguration.Text(rules, "credentialSendingTime").ShouldBe("activation");
		OnboardingRuleConfiguration.Boolean(rules, "sendStudentCredentialEmail").ShouldBeFalse();
		OnboardingRuleConfiguration.Boolean(rules, "sendGuardianCredentialEmail").ShouldBeFalse();
		OnboardingRuleConfiguration.Boolean(rules, "requireDocuments").ShouldBeTrue();
		saved.GetProperty("studentAccountAccess").GetString().ShouldBe(access);
	}
}
