using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace ShikshaMitra.Students;

public class AdmissionGender_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Table_readiness_is_nonmutating_and_publish_repairs_only_a_valid_draft(bool invalidOtherField)
	{
		var form = new AdmissionFormDefinition(Guid.NewGuid(), null, "Registration", "REGISTRATION", AdmissionFormLayoutType.SinglePage);
		var section = new AdmissionFormSectionDefinition(Guid.NewGuid(), form.Id, "STUDENT", "Student", 0, null);
		var legacy = "[{\"value\":\"Male\",\"label\":\"लड़का\"},{\"value\":\"Female\",\"label\":\"लड़की\"},{\"value\":\"OPTION_3\",\"label\":\"Other\"}]";
		var field = new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, section.Id, "GENDER", "Gender", AdmissionFormFieldType.Select,
			AdmissionFormBindingType.System, "Applicant.Gender", true, false, 0, null, null, "OPTION_3", null, null, legacy);
		AdmissionFormMetadataStore.WriteExperience(form, new CreateUpdateAdmissionFormDto { DefaultLanguage = "en", SupportedLanguages = ["en", "hi"],
			Sections = [new() { Code = section.Code, Fields = [new() { Code = field.Code, Translations = new() { ["hi"] = new() { Choices = new() { ["OPTION_3"] = "अन्य" } } } }] }] });
		var dependent = new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, section.Id, "DETAIL", "Details", AdmissionFormFieldType.Text,
			AdmissionFormBindingType.Custom, "DETAIL", false, false, 1, null, null, null, null, "{\"field\":\"GENDER\",\"operator\":\"Equals\",\"value\":\"OPTION_3\"}");
		var forms = Substitute.For<IRepository<AdmissionFormDefinition, Guid>>();
		forms.FindAsync(form.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(form);
		var sections = Substitute.For<IRepository<AdmissionFormSectionDefinition, Guid>>();
		sections.GetQueryableAsync().Returns(new[] { section }.AsQueryable());
		var fields = Substitute.For<IRepository<AdmissionFormFieldDefinition, Guid>>();
		var definitions = new List<AdmissionFormFieldDefinition> { field, dependent };
		if (invalidOtherField) definitions.Add(new AdmissionFormFieldDefinition(Guid.NewGuid(), form.Id, section.Id, "UNKNOWN", "Unknown",
			AdmissionFormFieldType.Select, AdmissionFormBindingType.Custom, "UNKNOWN", false, false, 1, null, null, null, null, null, "[]"));
		fields.GetQueryableAsync().Returns(definitions.AsQueryable());
		var provider = new ServiceCollection().AddSingleton<IAsyncQueryableExecuter>(new AsyncQueryableExecuter([]))
			.AddSingleton(Substitute.For<ICurrentUser>()).BuildServiceProvider();
		var service = new AdmissionFormAppService(forms, sections, fields, Substitute.For<IRepository<AdmissionOffering, Guid>>(),
			Substitute.For<IRepository<AdmissionApplication, Guid>>(), Substitute.For<IRepository<AdmissionPublicInquiryLink, Guid>>(), null!)
			{ LazyServiceProvider = new AbpLazyServiceProvider(provider) };
		(await service.ValidateAsync(form.Id)).IsValid.ShouldBe(!invalidOtherField);
		field.OptionsJson.ShouldBe(legacy);
		field.DefaultValue.ShouldBe("OPTION_3");
		form.Status.ShouldBe(AdmissionFormStatus.Draft);
		if (invalidOtherField)
		{
			await Should.ThrowAsync<BusinessException>(() => service.PublishAsync(form.Id, form.ConcurrencyStamp));
			field.OptionsJson.ShouldBe(legacy);
			form.Status.ShouldBe(AdmissionFormStatus.Draft);
			return;
		}
		(await Should.ThrowAsync<BusinessException>(() => service.PublishAsync(form.Id, "stale"))).Code.ShouldBe("Admissions.FormConcurrencyConflict");
		field.OptionsJson.ShouldBe(legacy);
		dependent.VisibilityRuleJson!.ShouldContain("OPTION_3");
		var published = await service.PublishAsync(form.Id, form.ConcurrencyStamp);
		published.Status.ShouldBe(AdmissionFormStatus.Published);
		var publishedGender = published.Sections.Single().Fields.Single(item => item.Code == field.Code);
		publishedGender.Id.ShouldBe(field.Id);
		publishedGender.Translations["hi"].Choices["Other"].ShouldBe("अन्य");
		field.OptionsJson!.ShouldNotContain("OPTION_3");
		field.OptionsJson!.ShouldContain("Other");
		field.DefaultValue.ShouldBe("Other");
		dependent.VisibilityRuleJson!.ShouldNotContain("OPTION_3");
		Should.Throw<BusinessException>(() => field.UpdateDraftChoices(form, legacy, null));
	}

	[Fact]
	public void Draft_save_repair_preserves_translations_defaults_and_linked_conditions()
	{
		var gender = new CreateUpdateAdmissionFormFieldDto { Code = "GENDER", BindingType = AdmissionFormBindingType.System,
			BindingKey = "Applicant.Gender", FieldType = AdmissionFormFieldType.Select, DefaultValue = "OPTION_3",
			OptionsJson = "[{\"value\":\"Male\",\"label\":\"Male\"},{\"value\":\"Female\",\"label\":\"Female\"},{\"value\":\"OPTION_3\",\"label\":\"Other\"}]",
			Translations = new() { ["hi"] = new() { Choices = new() { ["OPTION_3"] = "अन्य" } } } };
		var input = new CreateUpdateAdmissionFormDto { Sections = [new() { Code = "SECTION", Fields = [gender],
			VisibilityRuleJson = "{\"operator\":\"ANY\",\"conditions\":[{\"field\":\"GENDER\",\"operator\":\"In\",\"value\":[\"Male\",\"OPTION_3\"]},{\"field\":\"CUSTOM\",\"operator\":\"Equals\",\"value\":\"OPTION_3\"}]}" }] };
		AdmissionGenderChoices.NormalizeDraftDefinition(input);
		gender.DefaultValue.ShouldBe("Other");
		gender.Translations["hi"].Choices["Other"].ShouldBe("अन्य");
		using var parsed = System.Text.Json.JsonDocument.Parse(input.Sections[0].VisibilityRuleJson!);
		parsed.RootElement.GetProperty("conditions")[0].GetProperty("value")[1].GetString().ShouldBe("Other");
		parsed.RootElement.GetProperty("conditions")[1].GetProperty("value").GetString().ShouldBe("OPTION_3");
	}

	[Theory]
	[InlineData("In", "Male, OPTION_3", "Male,Other")]
	[InlineData("NotIn", "OPTION_3,Female", "Other,Female")]
	[InlineData("Contains", "Male,OPTION_3", "Male,OPTION_3")]
	public void Draft_rule_repair_preserves_supported_comma_list_semantics(string operation, string value, string expected)
	{
		var json = System.Text.Json.JsonSerializer.Serialize(new { field = "GENDER", @operator = operation, value });
		using var parsed = System.Text.Json.JsonDocument.Parse(AdmissionGenderChoices.NormalizeRule(json, new HashSet<string> { "GENDER" })!);
		parsed.RootElement.GetProperty("value").GetString().ShouldBe(expected);
	}

	[Theory]
	[InlineData(AdmissionFormStatus.Published)]
	[InlineData(AdmissionFormStatus.Archived)]
	public void Historical_definitions_are_not_reinterpreted_for_readiness(AdmissionFormStatus status)
	{
		var field = new AdmissionFormFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "GENDER", "Gender", AdmissionFormFieldType.Select,
			AdmissionFormBindingType.System, "Applicant.Gender", true, false, 0, null, null, null, null, null,
			"[{\"value\":\"Male\"},{\"value\":\"Female\"},{\"value\":\"OPTION_3\",\"label\":\"Other\"}]");
		AdmissionGenderChoices.ForValidation(field, status).ShouldBeSameAs(field);
	}
	[Theory]
	[InlineData(AdmissionFormBindingType.System, "Applicant.Gender", "Other", true)]
	[InlineData(AdmissionFormBindingType.Custom, "Applicant.Gender", "Other", false)]
	[InlineData(AdmissionFormBindingType.System, "Survey.Gender", "Other", false)]
	[InlineData(AdmissionFormBindingType.System, "Applicant.Gender", "Unspecified", false)]
	public void Draft_repair_is_restricted_to_the_known_generic_editor_mistake(AdmissionFormBindingType type, string key, string label, bool repaired)
	{
		var json = "[{\"value\":\"Male\",\"label\":\"लड़का\"},{\"value\":\"Female\",\"label\":\"लड़की\"},{\"value\":\"OPTION_3\",\"label\":\"" + label + "\"}]";
		var field = new CreateUpdateAdmissionFormFieldDto { BindingType = type, BindingKey = key,
			FieldType = AdmissionFormFieldType.Select, OptionsJson = json };
		var result = AdmissionGenderChoices.NormalizeDraftOptions(field);
		if (repaired)
		{
			using var parsed = System.Text.Json.JsonDocument.Parse(result!);
			parsed.RootElement[2].GetProperty("value").GetString().ShouldBe("Other");
			parsed.RootElement[0].GetProperty("label").GetString().ShouldBe("लड़का");
		}
		else result.ShouldBe(json);
	}

	[Fact]
	public void Other_answer_maps_from_registration_to_the_final_student_gender_without_changing_existing_values()
	{
		((int)GenderEnum.Male).ShouldBe(0);
		((int)GenderEnum.Female).ShouldBe(1);
		var applicant = new AdmissionApplicant(Guid.NewGuid(), null, Guid.NewGuid(), "Student", null, null,
			new DateTime(2016, 1, 1), null, null, Guid.NewGuid());
		AdmissionFormBindingRegistry.ApplyValue("Applicant.Gender", "Other", applicant, null).ShouldBeTrue();
		applicant.Gender.ShouldBe("Other");
		var gender = StudentCoreIdentityRules.ReadGender(applicant.Gender);
		gender.HasValue.ShouldBeTrue();
		((int)gender!.Value).ShouldBe(2);
		StudentCoreIdentityRules.IsComplete(applicant.FirstName, applicant.DateOfBirth, gender).ShouldBeTrue();
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), null, applicant.FirstName, null, null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			applicant.DateOfBirth!.Value, gender.Value, "9876543210");
		student.AdditionalDetail!.Gender.ShouldBe(gender);
	}

	[Fact]
	public void Applicant_profile_contract_accepts_canonical_other()
	{
		var validator = new UpdateAdmissionApplicantProfileDtoValidator();
		validator.Validate(new UpdateAdmissionApplicantProfileDto { FirstName = "Student",
			DateOfBirth = new DateTime(2016, 1, 1), Gender = "Other" }).IsValid.ShouldBeTrue();
		validator.Validate(new UpdateAdmissionApplicantProfileDto { FirstName = "Student",
			DateOfBirth = new DateTime(2016, 1, 1), Gender = "Unknown" }).IsValid.ShouldBeFalse();
	}

	[Theory]
	[InlineData("Other", false)]
	[InlineData("Unknown", true)]
	public void Inquiry_create_and_edit_validate_other_against_the_configured_choices(string value, bool invalid)
	{
		var section = new AdmissionFormSectionDefinition(Guid.NewGuid(), Guid.NewGuid(), "STUDENT", "Student", 0, null);
		var field = new AdmissionFormFieldDefinition(Guid.NewGuid(), section.FormDefinitionId, section.Id, "GENDER", "Gender",
			AdmissionFormFieldType.Select, AdmissionFormBindingType.System, "Applicant.Gender", true, false, 0,
			null, null, null, null, null, AdmissionFormBindingRegistry.Metadata["Applicant.Gender"].OptionsJson);
		var validator = new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator());
		validator.ValidateInquiry([field], [section], new Dictionary<string, string> { ["GENDER"] = value })
			.FieldErrors.ContainsKey("GENDER").ShouldBe(invalid);
	}

	[Theory]
	[InlineData("Other", false)]
	[InlineData("OPTION_3", true)]
	[InlineData("Unknown", true)]
	public void Submission_accepts_other_but_rejects_unknown_identity(string value, bool invalid)
	{
		var applicant = new AdmissionApplicant(Guid.NewGuid(), null, Guid.NewGuid(), "Student", null, null,
			new DateTime(2016, 1, 1), value, null, Guid.NewGuid());
		var field = new AdmissionFormFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "GENDER", "Gender",
			AdmissionFormFieldType.Select, AdmissionFormBindingType.System, "Applicant.Gender", true, false, 0,
			null, null, null, null, null, "[\"Male\",\"Female\",\"Other\"]");
		var result = new AdmissionFormValidationResult([], []);
		AdmissionSubmissionIdentityValidator.Validate(applicant, [field], new HashSet<string> { "GENDER" },
			new Dictionary<string, string> { ["GENDER"] = value }, result);
		result.FieldErrors.ContainsKey("GENDER").ShouldBe(invalid);
	}
}

