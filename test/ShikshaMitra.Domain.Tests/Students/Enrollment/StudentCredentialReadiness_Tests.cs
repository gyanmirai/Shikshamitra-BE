using System;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentCredentialReadiness_Tests
{
	[Theory]
	[InlineData("credentialSendingTime", "early")]
	[InlineData("credentialProvisioningMode", "sometimes")]
	[InlineData("guardianAccountRelationship", "everyone")]
	[InlineData("sendStudentCredentialEmail", "yes")]
	public void Invalid_credential_policy_is_rejected(string key, string value)
	{
		var rules = new System.Collections.Generic.Dictionary<string, object> { [key] = value };
		Should.Throw<BusinessException>(() => new OnboardingRuleConfiguration(Configuration(rules)));
	}

	[Theory]
	[InlineData("sendStudentCredentialEmail")]
	[InlineData("sendGuardianCredentialEmail")]
	public void Email_requires_corresponding_account_policy(string key)
	{
		Should.Throw<BusinessException>(() => new OnboardingRuleConfiguration(Configuration(new { sendStudentCredentialEmail = key == "sendStudentCredentialEmail", sendGuardianCredentialEmail = key == "sendGuardianCredentialEmail" })));
	}

	[Fact]
	public void Legacy_configuration_keeps_both_emails_disabled()
	{
		var settings = new OnboardingRuleConfiguration(Configuration(new { }));
		var rules = settings.Root.GetProperty("readinessAccess");
		OnboardingRuleConfiguration.Boolean(rules, "sendStudentCredentialEmail").ShouldBeFalse();
		OnboardingRuleConfiguration.Boolean(rules, "sendGuardianCredentialEmail").ShouldBeFalse();
	}

	[Fact]
	public void Override_records_exact_documents_staff_and_note_without_exempting_new_requirements()
	{
		var item = Case(); var document = Guid.NewGuid(); var staff = Guid.NewGuid();
		item.OverrideRequiredDocuments([document], "  Original unavailable; verified copy reviewed.  ", staff, DateTime.UtcNow);
		var saved = item.GetDocumentOverrides().Single();
		saved.DocumentTypeId.ShouldBe(document); saved.StaffUserId.ShouldBe(staff);
		saved.Reason.ShouldBe("Original unavailable; verified copy reviewed.");
		item.GetDocumentOverrides().Any(value => value.DocumentTypeId == Guid.NewGuid()).ShouldBeFalse();
		item.OverrideRequiredDocuments([document], "Updated verification", staff, DateTime.UtcNow);
		item.GetDocumentOverrides().Single().Reason.ShouldBe("Updated verification");
	}

	[Fact]
	public void Override_requires_note_documents_and_real_staff()
	{
		var item = Case();
		Should.Throw<ArgumentException>(() => item.OverrideRequiredDocuments([Guid.NewGuid()], " ", Guid.NewGuid(), DateTime.UtcNow));
		Should.Throw<BusinessException>(() => item.OverrideRequiredDocuments([], "Reviewed", Guid.NewGuid(), DateTime.UtcNow));
		Should.Throw<ArgumentException>(() => item.OverrideRequiredDocuments([Guid.NewGuid()], "Reviewed", Guid.Empty, DateTime.UtcNow));
	}

	[Fact]
	public void Cancelled_case_cannot_accept_a_document_override()
	{
		var item = Case(); item.Cancel("Withdrawn", DateTime.UtcNow);
		Should.Throw<BusinessException>(() => item.OverrideRequiredDocuments([Guid.NewGuid()], "Reviewed", Guid.NewGuid(), DateTime.UtcNow));
	}

	private static StudentOnboardingCase Case() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "credentials", null, DateTime.UtcNow.AddDays(7));
	private static string Configuration(object rules) => JsonSerializer.Serialize(new { readinessAccess = rules,
		onboardingProfiles = Array.Empty<object>(), grNumber = new { configurations = Array.Empty<object>() },
		rollNumber = new { configurations = Array.Empty<object>() }, divisionChange = new { configurations = Array.Empty<object>() } });
}
