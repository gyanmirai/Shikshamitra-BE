using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionFormInheritance_Tests
{
	[Fact]
	public void Legacy_forms_have_no_inheritance_policy()
	{
		AdmissionFormInheritanceMetadataStore.ReadPolicy(Form()).ShouldBeNull();
	}

	[Fact]
	public void Null_source_collections_return_a_business_validation_error()
	{
		Should.Throw<BusinessException>(() => AdmissionFormInheritanceMetadataStore.Write(Form(), new()
		{
			Inheritance = new() { Enabled = true, Sources = null! }
		})).Code.ShouldBe(AdmissionFormInheritanceErrorCodes.InvalidPolicy);
	}

	[Fact]
	public void Stored_malformed_policy_does_not_silently_fall_back_to_legacy()
	{
		var form = Form();
		form.SetProperty("AdmissionFormInheritanceV1", "{bad-json");
		Should.Throw<BusinessException>(() => AdmissionFormInheritanceMetadataStore.ReadPolicy(form))
			.Code.ShouldBe(AdmissionFormInheritanceErrorCodes.InvalidPolicy);
	}

	[Fact]
	public void Approved_inheritance_cannot_be_disabled_to_bypass_inherited_deletion_locks()
	{
		var form = Form();
		AdmissionFormInheritanceMetadataStore.Write(form, new()
		{
			Inheritance = new() { Enabled = true, Sources = [new() { InquiryFormDefinitionId = Guid.NewGuid() }] }
		});
		Should.Throw<BusinessException>(() => AdmissionFormInheritanceMetadataStore.Write(form, new()
		{
			Inheritance = new() { Enabled = false }
		}));
	}

	[Fact]
	public void Custom_identity_is_saved_and_copied_without_changing_question_meaning()
	{
		var form = Form();
		AdmissionFormInheritanceMetadataStore.Write(form, new()
		{
			Sections = [new() { Code = "DETAILS", Fields = [new() { Code = "QUESTION", Label = "A question", BindingType = AdmissionFormBindingType.Custom }] }]
		});
		var field = new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, Guid.NewGuid(), "QUESTION", "A question",
			AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, "QUESTION", false, false, 0, null, null, null, null, null);
		var identity = AdmissionFormInheritanceMetadataStore.Identity(form, field);
		var target = Form();
		AdmissionFormInheritanceMetadataStore.Copy(form, target);
		AdmissionFormInheritanceMetadataStore.Identity(target, field).ShouldBe(identity);
		identity.ShouldStartWith("custom:");
	}

	[Fact]
	public void Preview_keeps_origin_binding_and_source_labels_without_mutating_base_definition()
	{
		var registration = Definition(AdmissionFormPurpose.Registration, "Date of birth", "DATE");
		var inquiry = Definition(AdmissionFormPurpose.Inquiry, "DOB", "BIRTH");
		registration.Inheritance = new() { Enabled = true, Sources = [new() { InquiryFormDefinitionId = inquiry.Id }] };
		var result = AdmissionFormInheritanceService.Compose(registration, inquiry, true);
		var inherited = result.Definition.Sections.SelectMany(section => section.Fields).Single();

		result.IsValid.ShouldBeTrue();
		inherited.Label.ShouldBe("DOB");
		inherited.InheritedFrom!.SourceBindingKey.ShouldBe("Applicant.DateOfBirth");
		registration.Sections.Single().Fields.Single().Label.ShouldBe("Date of birth");
	}

	private static AdmissionFormDefinition Form() => new(Guid.NewGuid(), null, "Registration", "REG", AdmissionFormLayoutType.SinglePage);

	[Fact]
	public void Malformed_rule_in_an_unsaved_preview_reports_a_compatibility_issue()
	{
		var registration = Definition(AdmissionFormPurpose.Registration, "Date of birth", "DATE");
		registration.Sections.Single().Fields.Single().VisibilityRuleJson = "{broken";
		var inquiry = Definition(AdmissionFormPurpose.Inquiry, "DOB", "BIRTH");
		AdmissionFormInheritanceService.Compose(registration, inquiry, false).Issues.ShouldContain(issue => issue.Code == "InvalidDefinition");
	}

	[Fact]
	public void Duplicated_unsaved_codes_report_an_issue_instead_of_throwing()
	{
		var registration = Definition(AdmissionFormPurpose.Registration, "Date of birth", "DATE");
		registration.Sections.Single().Fields.Add(new() { Code = "DATE", Label = "Other", BindingType = AdmissionFormBindingType.Custom, BindingKey = "OTHER" });
		var inquiry = Definition(AdmissionFormPurpose.Inquiry, "DOB", "BIRTH");
		AdmissionFormInheritanceService.Compose(registration, inquiry, false).Issues.ShouldContain(issue => issue.Code == "InvalidDefinition");
	}

	[Fact]
	public void Registration_cannot_be_used_as_an_inquiry_source()
	{
		var registration = Definition(AdmissionFormPurpose.Registration, "Date of birth", "DATE");
		AdmissionFormInheritanceService.Compose(registration, registration, false).Issues.ShouldContain(issue => issue.Code == "InvalidSource");
	}

	[Fact]
	public void Approved_sources_cannot_be_removed_to_exclude_inherited_questions()
	{
		var form = Form();
		AdmissionFormInheritanceMetadataStore.Write(form, new()
		{
			Inheritance = new() { Enabled = true, Sources = [new() { InquiryFormDefinitionId = Guid.NewGuid() }] }
		});
		Should.Throw<BusinessException>(() => AdmissionFormInheritanceMetadataStore.Write(form, new()
		{
			Inheritance = new() { Enabled = true, Sources = [] }
		}));
	}

	[Fact]
	public void Merged_validation_bounds_are_checked_after_composition()
	{
		var registration = Definition(AdmissionFormPurpose.Registration, "Marks", "MARKS");
		var inquiry = Definition(AdmissionFormPurpose.Inquiry, "Score", "SCORE");
		var target = registration.Sections.Single().Fields.Single();
		var source = inquiry.Sections.Single().Fields.Single();
		foreach (var field in new[] { target, source })
		{
			field.BindingType = AdmissionFormBindingType.Custom;
			field.FieldIdentity = "custom:score";
			field.FieldType = AdmissionFormFieldType.Number;
		}
		target.ValidationJson = "{\"max\":5}";
		source.ValidationJson = "{\"min\":10}";
		var result = AdmissionFormInheritanceService.Compose(registration, inquiry, false);
		result.IsValid.ShouldBeFalse();
		result.Issues.ShouldContain(issue => issue.Message.Contains(AdmissionFormErrorCodes.InvalidValidationRule));
	}

	[Fact]
	public void Required_rule_cycles_created_by_merging_two_individually_valid_schemas_are_rejected()
	{
		var registration = Definition(AdmissionFormPurpose.Registration, "A", "A");
		var inquiry = Definition(AdmissionFormPurpose.Inquiry, "A", "A");
		foreach (var form in new[] { registration, inquiry })
		{
			var a = form.Sections.Single().Fields.Single();
			a.BindingType = AdmissionFormBindingType.Custom;
			a.FieldIdentity = "custom:A";
			a.FieldType = AdmissionFormFieldType.Text;
			form.Sections.Single().Fields.Add(new() { Id = Guid.NewGuid(), FormDefinitionId = form.Id, SectionId = a.SectionId,
				Code = "B", Label = "B", BindingType = AdmissionFormBindingType.Custom, BindingKey = "B", FieldIdentity = "custom:B" });
		}
		inquiry.Sections.Single().Fields.Single(field => field.Code == "A").RequiredRuleJson = "{\"field\":\"B\",\"operator\":\"IsNotEmpty\"}";
		registration.Sections.Single().Fields.Single(field => field.Code == "B").RequiredRuleJson = "{\"field\":\"A\",\"operator\":\"IsNotEmpty\"}";
		var result = AdmissionFormInheritanceService.Compose(registration, inquiry, false);
		result.IsValid.ShouldBeFalse();
		result.Issues.ShouldContain(issue => issue.Message.Contains(AdmissionFormErrorCodes.DependencyCycle));
	}

	[Fact]
	public void Custom_identity_is_derived_from_client_field_id_when_explicit_identity_is_omitted()
	{
		var fieldId = Guid.NewGuid();
		var input = new CreateUpdateAdmissionFormDto { Sections = [new() { Code = "DETAILS", Name = "Details",
			Fields = [new() { Id = fieldId, Code = "QUESTION", Label = "Question", BindingType = AdmissionFormBindingType.Custom, BindingKey = "QUESTION" }] }] };
		var form = Form();
		AdmissionFormInheritanceMetadataStore.Write(form, input);
		var savedField = new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, Guid.NewGuid(), "QUESTION", "Question",
			AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, "QUESTION", false, false, 0, null, null, null, null, null);
		var identity = AdmissionFormInheritanceMetadataStore.Identity(form, savedField);
		identity.ShouldBe("custom:" + fieldId.ToString("D"));
	}

	[Fact]
	public void Post_composition_validation_accepts_conditions_referencing_frozen_readonly_inquiry_context()
	{
		var registration = Definition(AdmissionFormPurpose.Registration, "Date of birth", "DATE");
		var inquiry = Definition(AdmissionFormPurpose.Inquiry, "DOB", "BIRTH");
		var source = inquiry.Sections.Single().Fields.Single();
		source.RequiredRuleJson = "{\"field\":\"CONTEXT\",\"operator\":\"IsNotEmpty\"}";
		inquiry.Sections.Single().Fields.Add(new()
		{
			Id = Guid.NewGuid(), FormDefinitionId = inquiry.Id, SectionId = source.SectionId,
			Code = "CONTEXT", Label = "Inquiry source", BindingType = AdmissionFormBindingType.System,
			BindingKey = "Inquiry.Source", FieldType = AdmissionFormFieldType.Text
		});
		var result = AdmissionFormInheritanceService.Compose(registration, inquiry, false);
		result.IsValid.ShouldBeTrue();
		result.InquiryContextSections.SelectMany(section => section.Fields).Single().ReadOnly.ShouldBeTrue();
	}

	private static AdmissionFormDto Definition(AdmissionFormPurpose purpose, string label, string code)
	{
		var id = Guid.NewGuid();
		var sectionId = Guid.NewGuid();
		return new()
		{
			Id = id, Purpose = purpose, Status = AdmissionFormStatus.Published,
			Sections = [new() { Id = sectionId, FormDefinitionId = id, Code = "DETAILS", Name = "Details",
				Fields = [new() { Id = Guid.NewGuid(), FormDefinitionId = id, SectionId = sectionId, Code = code, Label = label,
					FieldType = AdmissionFormFieldType.Date, BindingType = AdmissionFormBindingType.System, BindingKey = "Applicant.DateOfBirth" }] }]
		};
	}
}
