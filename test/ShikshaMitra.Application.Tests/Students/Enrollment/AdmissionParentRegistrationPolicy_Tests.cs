using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Application.Communication;
using ShikshaMitra.Communication;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionParentRegistrationPolicy_Tests
{
	[Fact]
	public void Repeat_row_visibility_cannot_authorize_a_hidden_cell_in_a_different_row()
	{
		var definition = RepeatedDefinition(); var validator = new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator());
		var saved = new List<AdmissionFormValueDto> { new() { FieldCode = "CTRL", ValueJson = "[\"yes\",\"no\"]" }, new() { FieldCode = "DETAIL", ValueJson = "[\"shown\",\"hidden\"]" } };
		var merged = AdmissionParentRegistrationForms.Merge(saved, [new() { FieldCode = "DETAIL", ValueJson = "[\"shown\",\"tampered\"]" }]);
		Should.Throw<BusinessException>(() => AdmissionParentRegistrationForms.CheckVisibleChanges(definition, saved, merged, new HashSet<string> { "DETAIL" }, validator));
		AdmissionParentRegistrationForms.CheckVisibleChanges(definition, saved, AdmissionParentRegistrationForms.Merge(saved, [new() { FieldCode = "DETAIL", ValueJson = "[\"updated\",\"hidden\"]" }]), new HashSet<string> { "DETAIL" }, validator);
	}
	[Fact]
	public void Readonly_repeat_controller_is_retained_for_editable_required_answer_validation()
	{
		var definition = RepeatedDefinition(); var validator = new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator());
		var values = new Dictionary<string, string> { ["CTRL"] = "[\"yes\",\"no\"]", ["DETAIL"] = "[\"\",\"\"]" };
		var result = validator.Validate(AdmissionEffectiveFormResolver.MaterializeFields(definition), AdmissionEffectiveFormResolver.MaterializeSections(definition),
			values, AdmissionEffectiveFormResolver.Experience(definition), new HashSet<string> { "DETAIL" });
		result.FieldErrors.ShouldContainKey("DETAIL[0]"); result.FieldErrors.ShouldNotContainKey("DETAIL[1]");
	}
	[Fact]
	public void A_controller_change_discards_obsolete_hidden_proposals_but_keeps_visible_repeat_answers()
	{
		var definition = RepeatedDefinition(); var validator = new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator());
		var saved = new List<AdmissionFormValueDto> { new() { FieldCode = "CTRL", ValueJson = "[\"yes\",\"yes\"]" }, new() { FieldCode = "DETAIL", ValueJson = "[\"old one\",\"old two\"]" } };
		var patch = AdmissionParentRegistrationForms.VisibleProposals(new(null!, definition, saved),
			[new() { FieldCode = "CTRL", ValueJson = "[\"no\",\"yes\"]" }, new() { FieldCode = "DETAIL", ValueJson = "[\"hidden proposal\",\"visible proposal\"]" }], validator);
		AdmissionApplicationValueConverter.Unwrap(patch.Single(value => value.FieldCode == "DETAIL").ValueJson).ShouldBe("[\"old one\",\"visible proposal\"]");
	}
	private static AdmissionFormDto RepeatedDefinition()
	{
		var id = Guid.NewGuid(); var section = Guid.NewGuid();
		return new() { Id = id, Sections = [new() { Id = section, Code = "ROWS", Name = "Rows", IsRepeatable = true, MinEntries = 0, MaxEntries = 3, Fields = [
			new() { Id = Guid.NewGuid(), SectionId = section, Code = "CTRL", Label = "Control", FieldType = AdmissionFormFieldType.Text, BindingType = AdmissionFormBindingType.Custom, ReadOnly = true },
			new() { Id = Guid.NewGuid(), SectionId = section, Code = "DETAIL", Label = "Detail", FieldType = AdmissionFormFieldType.Text, BindingType = AdmissionFormBindingType.Custom, Required = true,
				VisibilityRuleJson = "{\"operator\":\"Equals\",\"field\":\"CTRL\",\"value\":\"yes\"}" }] }] };
	}
	[Fact]
	public void Hidden_proposals_settle_cascading_controllers_before_staff_review()
	{
		var definition = new AdmissionFormDto { Sections = [new() { Fields = [new() { Code = "A" },
			new() { Code = "X", VisibilityRuleJson = "{\"operator\":\"Equals\",\"field\":\"A\",\"value\":\"true\"}" },
			new() { Code = "Y", VisibilityRuleJson = "{\"operator\":\"Equals\",\"field\":\"X\",\"value\":\"true\"}" }] }] };
		var saved = new List<AdmissionFormValueDto> { new() { FieldCode = "A", ValueJson = "\"false\"" }, new() { FieldCode = "X", ValueJson = "\"false\"" }, new() { FieldCode = "Y", ValueJson = "\"old\"" } };
		var proposed = new List<AdmissionFormValueDto> { new() { FieldCode = "A", ValueJson = "\"false\"" }, new() { FieldCode = "X", ValueJson = "\"true\"" }, new() { FieldCode = "Y", ValueJson = "\"new\"" } };
		// Give the materialized fields their actual shared section/form identity.
		definition.Id = Guid.NewGuid(); var section = definition.Sections.Single(); section.Id = Guid.NewGuid(); section.Code = "MAIN"; section.Name = "Main";
		foreach (var field in section.Fields) { field.Id = Guid.NewGuid(); field.SectionId = section.Id; field.Label = field.Code; field.FieldType = AdmissionFormFieldType.Text; }
		AdmissionParentRegistrationForms.VisibleProposals(new(null!, definition, saved), proposed, new(new AdmissionWorkflowConditionEvaluator())).ShouldBeEmpty();
	}
	[Fact]
	public void Hidden_correction_with_only_a_protected_controller_is_refused_but_safe_controller_is_allowed()
	{
		var definition = RepeatedDefinition(); definition.Sections[0].IsRepeatable = false;
		var saved = new List<AdmissionFormValueDto> { new() { FieldCode = "CTRL", ValueJson = "\"no\"" } };
		var validator = new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator());
		Should.Throw<BusinessException>(() => AdmissionParentRegistrationSchema.EnsureCorrectionReachable(definition, saved, ["DETAIL"], new HashSet<string> { "DETAIL" }, validator));
		AdmissionParentRegistrationSchema.EnsureCorrectionReachable(definition, saved, ["DETAIL"], new HashSet<string> { "CTRL", "DETAIL" }, validator);
	}
	[Fact]
	public void Accepted_either_original_is_complete_without_parent_upload()
	{
		var requirement = new AdmissionDocumentRequirement { Code = "ORIGINAL", Name = "Original", CollectionMode = "either" };
		var evidence = new AdmissionDocumentEvidence(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), requirement, 1, "original", Guid.NewGuid(),
			DateTime.UtcNow, "{}", originalSeenBy: Guid.NewGuid(), originalSeenAt: DateTime.UtcNow, notes: "Original inspected by staff.");
		AdmissionParentRegistrationDocuments.HasParentEvidence(new(requirement, "notConditional", true, "accepted", [evidence], [], [])).ShouldBeTrue();
	}
	[Fact]
	public void Invited_mother_can_capture_unlinked_father_but_cannot_rewrite_recipient_or_linked_identity()
	{
		var applicant = new AdmissionApplicant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Child", null, "Test", null, null, null, Guid.NewGuid());
		var father = new AdmissionFormFieldDto { Code = "FATHER", BindingType = AdmissionFormBindingType.System, BindingKey = "Father.Name" };
		AdmissionParentRegistrationSchema.CanWriteFor(father, "mother", applicant).ShouldBeTrue();
		AdmissionParentRegistrationSchema.CanWriteFor(father, "father", applicant).ShouldBeFalse();
		var phone = new AdmissionFormFieldDto { Code = "PHONE", BindingType = AdmissionFormBindingType.System, BindingKey = "Mother.Mobile" };
		AdmissionParentRegistrationSchema.CanWriteFor(phone, "mother", applicant).ShouldBeTrue();
		applicant.LinkStudent(Guid.NewGuid());
		AdmissionParentRegistrationSchema.CanWriteFor(phone, "mother", applicant).ShouldBeFalse();
		AdmissionParentRegistrationSchema.CanWriteFor(father, "mother", applicant).ShouldBeFalse();
	}
	[Fact]
	public void Public_schema_folds_private_rule_values_without_exposing_binding_or_audit_metadata()
	{
		var definition = new AdmissionFormDto { PublishedBy = Guid.NewGuid(), Sections = [new() { Code = "MAIN", Fields = [
			new() { Code = "SIBLING", BindingKey = "Family.SiblingStudentIds", BindingType = AdmissionFormBindingType.System },
			new() { Code = "NAME", BindingKey = "Applicant.FirstName", BindingType = AdmissionFormBindingType.System,
				RequiredRuleJson = "{\"operator\":\"IsNotEmpty\",\"field\":\"SIBLING\"}" }] }] };
		var projected = AdmissionParentRegistrationSchema.Project(definition, new HashSet<string> { "NAME" }, [new() { FieldCode = "SIBLING", ValueJson = "secret-student-id" }]);
		var json = JsonSerializer.Serialize(projected);
		json.ShouldNotContain("secret-student-id"); json.ShouldNotContain("PublishedBy"); json.ShouldNotContain("SIBLING");
		projected.Sections.Single().Fields.Single().RuntimeBindingKey.ShouldBe("Applicant.FirstName");
	}
	[Fact]
	public void Unknown_future_assessment_branch_does_not_collect_either_branch_documents()
	{
		var start = Guid.NewGuid(); var first = Guid.NewGuid(); var fallback = Guid.NewGuid(); var workflow = Guid.NewGuid();
		var route = AdmissionParentRegistrationDocuments.DefiniteRoute(start, [
			new(Guid.NewGuid(), workflow, start, first, 1, "{\"operator\":\"Equals\",\"field\":\"assessment.status\",\"value\":\"pending\"}"),
			new(Guid.NewGuid(), workflow, start, fallback, 2, isDefault: true)], new Dictionary<string, string> { ["assessment.status"] = "pending" });
		route.ShouldBe([start]);
	}
	[Theory]
	[InlineData("EmployeeIds")]
	[InlineData("StudentIds")]
	[InlineData("ClassIds")]
	[InlineData("DivisionIds")]
	[InlineData("OrganizationIds")]
	[InlineData("ParentGuardianIds")]
	public void Integration_campaign_rejects_every_extra_audience_expansion(string property)
	{
		var applicant = Guid.NewGuid(); var recipient = new AdmissionParentRecipientDto { FullName = "Mother", Email = "mother@example.test" };
		var audience = new CommunicationAudienceDefinitionDto { ExplicitRecipients = [new() { RecipientType = "AdmissionInquiry", RecipientId = applicant,
			DisplayName = recipient.FullName, Email = recipient.Email, IsParentDefault = true }] };
		((List<Guid>)typeof(CommunicationAudienceDefinitionDto).GetProperty(property)!.GetValue(audience)!).Add(Guid.NewGuid());
		Should.Throw<BusinessException>(() => CommunicationAppService.ValidateParentRegistrationAudience(audience, [CommunicationChannelType.Email], applicant, recipient));
	}
	[Fact]
	public void Integration_campaign_cannot_add_another_channel()
	{
		var applicant = Guid.NewGuid(); var recipient = new AdmissionParentRecipientDto { FullName = "Mother", Email = "mother@example.test" };
		var audience = new CommunicationAudienceDefinitionDto { ExplicitRecipients = [new() { RecipientType = "AdmissionInquiry", RecipientId = applicant,
			DisplayName = recipient.FullName, Email = recipient.Email, IsParentDefault = true }] };
		Should.Throw<BusinessException>(() => CommunicationAppService.ValidateParentRegistrationAudience(audience, [CommunicationChannelType.Email, CommunicationChannelType.Sms], applicant, recipient));
	}
}
