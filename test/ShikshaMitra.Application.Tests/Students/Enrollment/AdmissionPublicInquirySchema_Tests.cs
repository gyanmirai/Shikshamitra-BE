using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPublicInquirySchema_Tests
{
	[Fact]
	public void Projection_clones_the_source_and_preserves_compound_rules_with_false_and_empty_defaults()
	{
		var form = Form(
			Field("SINGLE", "Family.IsSingleChild"), Field("IDS", "Family.SiblingStudentIds"),
			Field("OTHER", "Family.IsSingleParentChild"), Field("ANSWER", null));
		const string compound = "{\"operator\":\"ANY\",\"conditions\":[{\"field\":\"SINGLE\",\"operator\":\"equals\",\"value\":true},{\"operator\":\"ALL\",\"conditions\":[{\"field\":\"IDS\",\"operator\":\"IsEmpty\"},{\"field\":\"OTHER\",\"operator\":\"equals\",\"value\":\"yes\"}]}]}";
		form.Sections[0].VisibilityRuleJson = compound;
		form.Sections[0].StepVisibilityRuleJson = "{\"field\":\"SINGLE\",\"operator\":\"equals\",\"value\":false}";
		form.Sections[0].Fields.Last().RequiredRuleJson = compound;
		var before = JsonSerializer.Serialize(form);
		var result = AdmissionPublicInquirySchema.Project(form);
		JsonSerializer.Serialize(form).ShouldBe(before);
		result.Sections[0].Fields.Select(field => field.Code).ShouldBe(new[] { "OTHER", "ANSWER" });
		var evaluator = new AdmissionWorkflowConditionEvaluator();
		evaluator.Evaluate(result.Sections[0].VisibilityRuleJson, new Dictionary<string, string> { ["OTHER"] = "yes" }).ShouldBeTrue();
		evaluator.Evaluate(result.Sections[0].VisibilityRuleJson, new Dictionary<string, string> { ["OTHER"] = "no" }).ShouldBeFalse();
		evaluator.Evaluate(result.Sections[0].StepVisibilityRuleJson, new Dictionary<string, string>()).ShouldBeTrue();
		result.Sections[0].Fields.Last().RequiredRuleJson.ShouldNotBeNull().ShouldContain("OTHER");
	}

	[Fact]
	public void Omitted_list_numeric_rule_uses_the_frontend_empty_string_zero_policy()
	{
		var source = Form(Field("IDS", "Family.SiblingStudentIds"), Field("DETAIL", null));
		source.Sections[0].Fields.Last().RequiredRuleJson = "{\"field\":\"IDS\",\"operator\":\"GreaterThanOrEqual\",\"value\":0}";
		var projected = AdmissionPublicInquirySchema.Project(source);
		new AdmissionWorkflowConditionEvaluator().Evaluate(projected.Sections[0].Fields.Single().RequiredRuleJson, new Dictionary<string, string>()).ShouldBeTrue();
	}

	[Fact]
	public void Projection_uses_exact_markers_and_aliases_while_registered_ownership_wins()
	{
		var dynamic = Field("DYNAMIC", null); dynamic.OptionsJson = "{\"source\":\"StudentSiblings\"}";
		var similar = Field("SIMILAR", null); similar.OptionsJson = "{\"source\":\"studentsiblings\"}";
		var malformed = Field("MALFORMED", null); malformed.OptionsJson = "{";
		malformed.VisibilityRuleJson = "{";
		var source = Form(Field("SINGLE_CHILD", "Applicant.FirstName"), Field("SIBLING_STUDENT", "Legacy.Unknown"),
			Field("CUSTOM_SIBLING", null), Field("FLAG", "family.siblingatschool"), dynamic, similar, malformed);
		var result = AdmissionPublicInquirySchema.Project(source);
		result.Sections[0].Fields.Select(field => field.Code).ShouldBe(new[] { "SINGLE_CHILD", "CUSTOM_SIBLING", "SIMILAR", "MALFORMED" });
		result.Sections[0].Fields.Last().VisibilityRuleJson.ShouldBe("{");
	}

	[Fact]
	public void Captured_source_is_versioned_tenant_bound_and_unmarked_legacy_is_unchanged()
	{
		var inquiry = new AdmissionInquiry(Guid.NewGuid(), Guid.NewGuid(), "INQ-TEST", Guid.NewGuid(), null, "Child", "9876543210", null, AdmissionInquirySource.QrCode, null);
		AdmissionPublicInquirySchema.ReadCaptured(inquiry).ShouldBeNull();
		var source = Form(Field("PUBLIC", null));
		inquiry.RecordFormResponseSnapshot("{}", source.Id);
		AdmissionPublicInquirySchema.Capture(inquiry, source);
		AdmissionPublicInquirySchema.ReadCaptured(inquiry)!.Id.ShouldBe(source.Id);
		var foreign = JsonSerializer.SerializeToNode(new { formatVersion = 1, tenantId = Guid.NewGuid(), definition = source });
		inquiry.SetProperty("AdmissionPublicInquiryEffectiveSource", foreign!.ToJsonString());
		Should.Throw<BusinessException>(() => AdmissionPublicInquirySchema.ReadCaptured(inquiry)).Code.ShouldBe(AdmissionFormInheritanceErrorCodes.InvalidSnapshot);
		inquiry.SetProperty("AdmissionPublicInquiryEffectiveSource", "{\"formatVersion\":2}");
		Should.Throw<BusinessException>(() => AdmissionPublicInquirySchema.ReadCaptured(inquiry)).Code.ShouldBe(AdmissionFormInheritanceErrorCodes.InvalidSnapshot);
	}

	[Fact]
	public void Canonical_fingerprint_sorts_projected_answers_normalizes_email_and_excludes_request_id()
	{
		var email = Field("EMAIL", "Father.Email"); email.FieldType = AdmissionFormFieldType.Email;
		var phone = Field("PHONE", "Father.Mobile"); phone.FieldType = AdmissionFormFieldType.Phone;
		var source = Form(email, phone, Field("CHILD", "Applicant.Name"));
		var first = new SubmitPublicAdmissionInquiryDto { AdmissionOfferingId = Guid.NewGuid(), RequestId = Guid.NewGuid(), GuardianPhone = "98765 43210", FormValues = [new() { FieldCode = "EMAIL", ValueJson = "\" Father@Example.com \"" }, new() { FieldCode = "PHONE", ValueJson = "\"98765 43210\"" }, new() { FieldCode = "CHILD", ValueJson = "\"Child\"" }] };
		var second = JsonSerializer.Deserialize<SubmitPublicAdmissionInquiryDto>(JsonSerializer.Serialize(first))!;
		second.RequestId = Guid.NewGuid(); second.FormValues.Reverse();
		second.GuardianPhone = "9876543210";
		second.FormValues.Single(value => value.FieldCode == "PHONE").ValueJson = "\"9876543210\"";
		second.FormValues.Single(value => value.FieldCode == "EMAIL").ValueJson = "\"father@example.com\"";
		AdmissionPublicInquirySubmissionStore.Fingerprint(first, source).ShouldBe(AdmissionPublicInquirySubmissionStore.Fingerprint(second, source));
		second.FormValues.Single(value => value.FieldCode == "CHILD").ValueJson = "\"Other child\"";
		AdmissionPublicInquirySubmissionStore.Fingerprint(first, source).ShouldNotBe(AdmissionPublicInquirySubmissionStore.Fingerprint(second, source));
	}

	private static AdmissionFormFieldDto Field(string code, string? binding) => new() { Id = Guid.NewGuid(), Code = code, Label = "Sibling question", BindingKey = binding };
	private static AdmissionFormDto Form(params AdmissionFormFieldDto[] fields)
	{
		var id = Guid.NewGuid(); var sectionId = Guid.NewGuid();
		foreach (var field in fields) { field.FormDefinitionId = id; field.SectionId = sectionId; }
		return new() { Id = id, Purpose = AdmissionFormPurpose.Inquiry, Sections = [new() { Id = sectionId, FormDefinitionId = id, Code = "PUBLIC", Name = "Public", Fields = fields.ToList() }] };
	}
}
