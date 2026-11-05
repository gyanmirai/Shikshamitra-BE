using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionCoreFieldComposition_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Versioned_inherited_full_name_remains_the_only_editable_name_and_does_not_modify_sources(bool reservedLegacyAlias)
	{
		var name = Field("Applicant.Name", AdmissionFormFieldType.Text, true);
		if (reservedLegacyAlias) { name.Code = "STUDENT_NAME"; name.BindingType = AdmissionFormBindingType.Custom; name.BindingKey = "STUDENT_NAME"; }
		var inquiry = Inquiry(name);
		var registration = Registration(inquiry.Id);
		var before = JsonSerializer.Serialize(inquiry);
		var result = AdmissionFormInheritanceService.Compose(registration, inquiry, true);
		result.Issues.ShouldBeEmpty();
		result.Definition.CoreFieldPolicyVersion.ShouldBe(1);
		var fields = result.Definition.Sections.SelectMany(section => section.Fields).ToList();
		fields.ShouldNotContain(field => field.BindingKey == "Applicant.FirstName" || field.BindingKey == "Applicant.MiddleName" || field.BindingKey == "Applicant.LastName");
		var full = fields.Single(field => field.InheritedFrom != null && field.InheritedFrom.SourceFieldCode == name.Code);
		full.Required.ShouldBeTrue(); full.ReadOnly.ShouldBeFalse(); full.Label.ShouldBe("Original Applicant.Name");
		JsonSerializer.Serialize(inquiry).ShouldBe(before);
	}

	[Fact]
	public void Optional_source_dob_and_gender_are_required_in_registration_when_registration_requires_them()
	{
		var gender = Field("Applicant.Gender", AdmissionFormFieldType.Select, false);
		gender.OptionsJson = "[{\"value\":\"Male\",\"label\":\"Male\"},{\"value\":\"Female\",\"label\":\"Female\"},{\"value\":\"Other\",\"label\":\"Other\"}]";
		var inquiry = Inquiry(Field("Applicant.DateOfBirth", AdmissionFormFieldType.Date, false), gender);
		var inquiryBefore = JsonSerializer.Serialize(inquiry);
		var result = AdmissionFormInheritanceService.Compose(Registration(inquiry.Id), inquiry, true);
		result.Issues.ShouldBeEmpty();
		var fields = result.Definition.Sections.SelectMany(section => section.Fields).ToList();
		fields.Count(field => field.BindingKey == "Applicant.DateOfBirth").ShouldBe(1);
		fields.Count(field => field.BindingKey == "Applicant.Gender").ShouldBe(1);
		fields.Single(field => field.BindingKey == "Applicant.DateOfBirth").Required.ShouldBeTrue();
		fields.Single(field => field.BindingKey == "Applicant.Gender").Required.ShouldBeTrue();
		fields.Single(field => field.BindingKey == "Applicant.DateOfBirth").InheritedFrom!.SourceRequired.ShouldBeFalse();
		JsonSerializer.Serialize(inquiry).ShouldBe(inquiryBefore);
	}

	[Fact]
	public void Historical_registration_keeps_its_optional_source_questions_without_new_policy_validation()
	{
		var inquiry = Inquiry(Field("Applicant.DateOfBirth", AdmissionFormFieldType.Date, false));
		var registration = Registration(inquiry.Id); registration.CoreFieldPolicyVersion = null;
		var result = AdmissionFormInheritanceService.Compose(registration, inquiry, true);
		result.Issues.ShouldBeEmpty();
		result.Definition.CoreFieldPolicyVersion.ShouldBeNull();
	}

	[Fact]
	public void Retained_application_form_projection_preserves_the_core_policy_marker()
	{
		var form = new AdmissionFormDefinition(Guid.NewGuid(), Guid.NewGuid(), "Registration", "REGISTRATION", AdmissionFormLayoutType.SinglePage);
		AdmissionFormMetadataStore.WriteExperience(form, new() { CoreFieldPolicyVersion = 1 });
		AdmissionApplicationFormService.MapDefinition(form, [], []).CoreFieldPolicyVersion.ShouldBe(1);
	}
	private static AdmissionFormDto Registration(Guid source)
	{
		var input = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Registration };
		AdmissionCoreFieldPolicy.PrepareNew(input);
		foreach (var section in input.Sections) { section.Id = Guid.NewGuid(); foreach (var field in section.Fields) { field.Id = Guid.NewGuid(); field.SectionId = section.Id; } }
		var definition = JsonSerializer.Deserialize<AdmissionFormDto>(JsonSerializer.Serialize(input))!;
		definition.Id = Guid.NewGuid(); definition.Status = AdmissionFormStatus.Draft;
		definition.Inheritance = new() { Enabled = true, Sources = [new() { InquiryFormDefinitionId = source }] };
		CompleteIds(definition);
		return definition;
	}
	private static AdmissionFormDto Inquiry(params AdmissionFormFieldDto[] fields)
	{
		var form = new AdmissionFormDto { Id = Guid.NewGuid(), Purpose = AdmissionFormPurpose.Inquiry, Status = AdmissionFormStatus.Published,
			Sections = [new() { Code = "SOURCE", Name = "Source", Fields = fields.ToList() }] };
		CompleteIds(form); return form;
	}
	private static AdmissionFormFieldDto Field(string key, AdmissionFormFieldType type, bool required) => new()
		{ Code = key.Replace('.', '_').ToUpperInvariant(), Label = "Original " + key, BindingType = AdmissionFormBindingType.System, BindingKey = key, Required = required, FieldType = type };
	private static void CompleteIds(AdmissionFormDto definition)
	{
		foreach (var section in definition.Sections)
		{
			section.Id = Guid.NewGuid(); section.FormDefinitionId = definition.Id;
			foreach (var field in section.Fields) { field.Id = Guid.NewGuid(); field.FormDefinitionId = definition.Id; field.SectionId = section.Id; }
		}
	}
}

