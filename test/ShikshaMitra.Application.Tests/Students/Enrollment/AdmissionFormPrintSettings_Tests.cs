using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormPrintSettings_Tests
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private const string Spacious = """{"template":"boxed-grid","layout":"spacious","paperSize":"A4","showSchoolHeader":false,"showPhoto":false,"showSignature":false}""";

	[Fact]
	public void Persisted_print_answers_are_empty_when_only_editor_suggestions_exist()
	{
		var form = new AdmissionApplicationFormDto
		{
			Values = [new() { FieldCode = "RELATION", ValueJson = "\"Father\"" }],
			AutoFilledFieldCodes = ["RELATION"]
		};
		var json = JsonSerializer.SerializeToNode(form, JsonOptions)!;
		json["persistedValues"].ShouldNotBeNull();
		json["persistedValues"]!.AsArray().ShouldBeEmpty();
		json["values"]!.AsArray().Count.ShouldBe(1);
	}

	[Fact]
	public void Persisted_print_answers_roundtrip_separately_from_editor_suggestions()
	{
		var form = JsonSerializer.Deserialize<AdmissionApplicationFormDto>("""
			{"values":[{"fieldCode":"RELATION","valueJson":"\"Father\""}],"persistedValues":[{"fieldCode":"NAME","valueJson":"\"Saved Name\""}]}
			""", JsonOptions)!;
		var json = JsonSerializer.SerializeToNode(form, JsonOptions)!;
		json["persistedValues"].ShouldNotBeNull();
		json["persistedValues"]![0]!["fieldCode"]!.GetValue<string>().ShouldBe("NAME");
		json["persistedValues"]![0]!["valueJson"]!.GetValue<string>().ShouldBe("\"Saved Name\"");
		json["values"]![0]!["fieldCode"]!.GetValue<string>().ShouldBe("RELATION");
	}

	[Fact]
	public void Legacy_definition_returns_print_defaults_to_both_consumers()
	{
		var form = Form();
		AssertSettings(AdmissionFormAppService.Map(form, [], []), "compact", true);
		AssertSettings(AdmissionApplicationFormService.MapDefinition(form, [], []), "compact", true);
		form.SetProperty("AdmissionFormExperience", """{"printSettings":{"layout":"spacious"}}""");
		AssertSettings(AdmissionFormAppService.Map(form, [], []), "spacious", true);
	}

	[Fact]
	public void Settings_roundtrip_and_legacy_update_retains_saved_print_layout()
	{
		var form = Form();
		AdmissionFormMetadataStore.WriteExperience(form, Input(Spacious));
		AssertSettings(AdmissionFormAppService.Map(form, [], []), "spacious", false, "boxed-grid");
		AdmissionFormMetadataStore.WriteExperience(form, new() { Name = "Updated by older client" });
		AssertSettings(AdmissionApplicationFormService.MapDefinition(form, [], []), "spacious", false, "boxed-grid");
		var legacySettings = Input("""{"layout":"compact"}""");
		AdmissionFormMetadataStore.WriteExperience(form, legacySettings);
		legacySettings.PrintSettings!.Template.ShouldBeNull();
		AssertSettings(AdmissionFormAppService.Map(form, [], []), "compact", true, "boxed-grid");
		AdmissionFormMetadataStore.WriteExperience(form, Input("""{"template":"structured-classic"}"""));
		AssertSettings(AdmissionFormAppService.Map(form, [], []), "compact", true, "structured-classic");
	}

	[Fact]
	public void Address_layout_roundtrips_and_invalid_choices_are_rejected()
	{
		var form = Form();
		AdmissionFormMetadataStore.WriteExperience(form, Input("""{"advanced":{"header":{"addressLayout":"horizontal"}}}"""));
		AdmissionFormAppService.Map(form, [], []).PrintSettings.Advanced!.Header!.AddressLayout.ShouldBe("horizontal");
		Should.Throw<BusinessException>(() => AdmissionFormMetadataStore.WriteExperience(
			form, Input("""{"advanced":{"header":{"addressLayout":"diagonal"}}}""")));
		AdmissionFormAppService.Map(form, [], []).PrintSettings.Advanced!.Header!.AddressLayout.ShouldBe("horizontal");
	}

	[Fact]
	public void Copy_used_for_clone_and_new_version_retains_independent_settings()
	{
		var source = Form();
		AdmissionFormMetadataStore.WriteExperience(source, Input(Spacious));
		var target = Form();
		AdmissionFormMetadataStore.CopyExperience(source, target);
		AdmissionFormMetadataStore.WriteExperience(source, Input("""{"layout":"compact"}"""));
		AssertSettings(AdmissionFormAppService.Map(target, [], []), "spacious", false, "boxed-grid");
	}

	[Theory]
	[InlineData("{\"layout\":\"landscape\"}")]
	[InlineData("{\"layout\":null}")]
	[InlineData("{\"paperSize\":\"Letter\"}")]
	[InlineData("{\"paperSize\":null}")]
	[InlineData("{\"template\":\"unsupported\"}")]
	[InlineData("{\"template\":\"\"}")]
	public void Unsupported_settings_are_rejected_before_metadata_is_changed(string settings)
	{
		var form = Form();
		Should.Throw<BusinessException>(() => AdmissionFormMetadataStore.WriteExperience(form, Input(settings)))
			.Code.ShouldBe(AdmissionFormErrorCodes.InvalidDefinition);
		form.ExtraProperties.ShouldBeEmpty();
	}

	[Fact]
	public void Inheritance_and_frozen_application_use_registration_print_settings()
	{
		var registration = Definition(AdmissionFormPurpose.Registration, Spacious);
		var inquiry = Definition(AdmissionFormPurpose.Inquiry, """{"layout":"compact"}""");
		var preview = AdmissionFormInheritanceService.Compose(registration, inquiry, false);
		preview.IsValid.ShouldBeTrue();
		AssertSettings(preview.Definition, "spacious", false, "boxed-grid");
		var instance = new AdmissionFormInstance(Guid.NewGuid(), null, Guid.NewGuid(), registration.Id, registration.Version);
		AdmissionEffectiveFormStore.Freeze(instance, preview);
		var changed = JsonSerializer.SerializeToNode(preview.Definition, JsonOptions)!;
		changed["printSettings"] = JsonNode.Parse("""{"layout":"compact"}""");
		preview.Definition = changed.Deserialize<AdmissionFormDto>(JsonOptions)!;
		AssertSettings(AdmissionEffectiveFormStore.Read(instance)!.Definition, "spacious", false, "boxed-grid");
	}

	[Fact]
	public void Legacy_frozen_application_defaults_to_simple_without_rewriting_its_snapshot()
	{
		var definition = Definition(AdmissionFormPurpose.Registration, """{"layout":"spacious"}""");
		var instance = new AdmissionFormInstance(Guid.NewGuid(), null, Guid.NewGuid(), definition.Id, definition.Version);
		AdmissionEffectiveFormStore.Freeze(instance, new() { Definition = definition });
		var frozenJson = instance.GetProperty<string>(AdmissionFormInstance.EffectiveSchemaKey);
		AssertSettings(AdmissionEffectiveFormStore.Read(instance)!.Definition, "spacious", true);
		instance.GetProperty<string>(AdmissionFormInstance.EffectiveSchemaKey).ShouldBe(frozenJson);
	}

	[Fact]
	public void Version_comparison_treats_legacy_template_as_simple_but_reports_a_template_change()
	{
		var before = Definition(AdmissionFormPurpose.Registration, """{}""");
		var after = JsonSerializer.Deserialize<AdmissionFormDto>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
		after.PrintSettings.Template = "simple";
		AdmissionFormVersionComparer.Compare(before, after).ShouldBeEmpty();
		after.PrintSettings.Template = "structured-classic";
		AdmissionFormVersionComparer.Compare(before, after).ShouldContain("Print settings changed");
	}

	[Fact]
	public void Version_comparison_reports_print_presentation_changes()
	{
		var before = Definition(AdmissionFormPurpose.Registration, """{"layout":"compact"}""");
		var after = JsonSerializer.Deserialize<AdmissionFormDto>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
		var changed = JsonSerializer.SerializeToNode(after, JsonOptions)!;
		changed["printSettings"] = JsonNode.Parse(Spacious);
		after = changed.Deserialize<AdmissionFormDto>(JsonOptions)!;
		AdmissionFormVersionComparer.Compare(before, after).ShouldContain("Print settings changed");
	}

	private static AdmissionFormDefinition Form() => new(Guid.NewGuid(), null, "Registration", "REG", AdmissionFormLayoutType.SinglePage);
	private static CreateUpdateAdmissionFormDto Input(string settings) =>
		JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>("{\"printSettings\":" + settings + "}", JsonOptions)!;
	private static AdmissionFormDto Definition(AdmissionFormPurpose purpose, string settings)
	{
		var id = Guid.NewGuid();
		var sectionId = Guid.NewGuid();
		var definition = JsonSerializer.Deserialize<AdmissionFormDto>("{\"printSettings\":" + settings + "}", JsonOptions)!;
		definition.Id = id;
		definition.Version = 1;
		definition.Purpose = purpose;
		definition.Sections = [new() { Id = sectionId, FormDefinitionId = id, Code = "DETAILS", Name = "Details",
			Fields = [new() { Id = Guid.NewGuid(), FormDefinitionId = id, SectionId = sectionId, Code = "NAME", Label = "Name",
				FieldType = AdmissionFormFieldType.Text, BindingType = AdmissionFormBindingType.System, BindingKey = "Applicant.Name" }] }];
		return definition;
	}
	private static void AssertSettings(AdmissionFormDto definition, string layout, bool enabled, string template = "simple")
	{
		var settings = JsonSerializer.SerializeToNode(definition, JsonOptions)!["printSettings"];
		settings.ShouldNotBeNull();
		settings["template"]!.GetValue<string>().ShouldBe(template);
		settings["layout"]!.GetValue<string>().ShouldBe(layout);
		settings["paperSize"]!.GetValue<string>().ShouldBe("A4");
		settings["showSchoolHeader"]!.GetValue<bool>().ShouldBe(enabled);
		settings["showPhoto"]!.GetValue<bool>().ShouldBe(enabled);
		settings["showSignature"]!.GetValue<bool>().ShouldBe(enabled);
	}
}
