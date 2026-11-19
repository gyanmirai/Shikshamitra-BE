using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentCredentialsValidation_Tests
{
	[Theory]
	[InlineData("student", "self")]
	[InlineData("guardian", "father")]
	[InlineData("guardian", "mother")]
	[InlineData("guardian", "guardian")]
	public void One_click_contract_accepts_only_the_selected_saved_owner_shape(string type, string relationship)
	{
		new SendStudentCredentialsDtoValidator().Validate(new SendStudentCredentialsDto { AccountType = type, Relationship = relationship,
			ConfirmRecipientAccess = type == "guardian", ExpectedEmail = "owner@example.test", ConcurrencyStamp = Guid.NewGuid().ToString("N") }).IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData("student", "father", "owner@example.test", "stamp")]
	[InlineData("guardian", "self", "owner@example.test", "stamp")]
	[InlineData("staff", "self", "owner@example.test", "stamp")]
	[InlineData("student", "self", "not-an-email", "stamp")]
	[InlineData("student", "self", "owner@example.test", "")]
	public void One_click_contract_rejects_invalid_identity_or_missing_drift_guards(string type, string relationship, string email, string stamp)
	{
		new SendStudentCredentialsDtoValidator().Validate(new SendStudentCredentialsDto { AccountType = type, Relationship = relationship,
			ExpectedEmail = email, ConcurrencyStamp = stamp }).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void One_click_contract_contains_only_saved_owner_guards_and_explicit_access_intents()
	{
		typeof(SendStudentCredentialsDto).GetProperties().Select(property => property.Name)
			.ShouldBe(["SendExistingParentHelp", "AccountType", "Relationship", "ExpectedEmail", "ConcurrencyStamp", "ConfirmRecipientAccess"]);
	}
	[Theory]
	[InlineData("father")]
	[InlineData("mother")]
	[InlineData("guardian")]
	public void Adult_credentials_and_existing_parent_help_require_explicit_access_confirmation(string relationship)
	{
		foreach (var help in new[] { false, true })
			new SendStudentCredentialsDtoValidator().Validate(new SendStudentCredentialsDto { AccountType = "guardian", Relationship = relationship,
				ExpectedEmail = "parent@example.test", ConcurrencyStamp = "stamp", SendExistingParentHelp = help }).IsValid.ShouldBeFalse();
	}

	[Theory]
	[InlineData("{\"email\":\"guardian@example.test\"}", "{\"email\":\"student@example.test\"}", true)]
	[InlineData("{\"Email\":null}", "{\"Email\":\"student@example.test\"}", true)]
	[InlineData("{\"email\":\"student@example.test\"}", "{\"email\":\"STUDENT@example.test \"}", false)]
	[InlineData("{\"email\":\"guardian@example.test\"}", "{\"email\":\"guardian@example.test\"}", false)]
	[InlineData("{}", "{}", false)]
	[InlineData("invalid", "{\"email\":\"student@example.test\"}", false)]
	public void Copied_general_payloads_do_not_establish_student_email_provenance(string before, string after, bool expected)
	{
		StudentCredentialsRecipientReader.ChangedToEmail(before, after, "student@example.test").ShouldBe(expected);
	}
	[Theory]
	[InlineData("Applicant.Email", true, true, true)]
	[InlineData("Applicant.Email", true, false, false)]
	[InlineData("Guardian.Email", true, true, false)]
	[InlineData("Applicant.Email", false, true, false)]
	public void Retained_form_provenance_requires_a_changed_system_student_email_binding(string binding, bool system, bool changed, bool expected)
	{
		var source = new StudentRecordRegistrationSourceDto { ApplicationId = Guid.NewGuid(), FormInstanceId = Guid.NewGuid(),
			SubmissionId = Guid.NewGuid(), FormDefinitionId = Guid.NewGuid() };
		var definition = new AdmissionFormDto { Id = source.FormDefinitionId, Sections = [new() { Fields = [new()
			{ Code = "CONTACT_EMAIL", BindingKey = binding, BindingType = system ? AdmissionFormBindingType.System : AdmissionFormBindingType.Custom }] }] };
		StudentRecordCustomDetailsPayload Payload(string value) => new() { Sources = [new() { ApplicationId = source.ApplicationId,
			FormInstanceId = source.FormInstanceId, SubmissionId = source.SubmissionId,
			Values = new Dictionary<string, JsonElement> { ["CONTACT_EMAIL"] = JsonSerializer.SerializeToElement(value) } }] };
		StudentCredentialsRecipientReader.ChangedFormToEmail(JsonSerializer.Serialize(Payload(changed ? "guardian@example.test" : "student@example.test")),
			JsonSerializer.Serialize(Payload("student@example.test")), "student@example.test", JsonSerializer.Serialize(new[] { source }),
			JsonSerializer.Serialize(new[] { new { source.SubmissionId, Definition = definition } })).ShouldBe(expected);
	}
}
