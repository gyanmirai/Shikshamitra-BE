using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionEffectiveFormStore_Tests
{
	[Fact]
	public void Legacy_instance_has_no_effective_schema()
	{
		AdmissionEffectiveFormStore.Read(Instance()).ShouldBeNull();
	}

	[Fact]
	public void Missing_snapshot_markers_cannot_downgrade_an_enabled_base_to_legacy_behavior()
	{
		var definition = new AdmissionFormDefinition(Guid.NewGuid(), null, "Registration", "REGISTRATION", AdmissionFormLayoutType.SinglePage);
		AdmissionFormInheritanceMetadataStore.Write(definition, new()
		{
			Purpose = AdmissionFormPurpose.Registration,
			Inheritance = new() { Enabled = true }
		});

		Should.Throw<BusinessException>(() => AdmissionEffectiveFormStore.EnsureLegacyAllowed(definition))
			.Code.ShouldBe(AdmissionFormInheritanceErrorCodes.InvalidSnapshot);
	}

	[Fact]
	public void Frozen_schema_keeps_source_labels_after_input_changes()
	{
		var instance = Instance();
		var preview = Preview(instance);
		AdmissionEffectiveFormStore.Freeze(instance, preview);
		preview.Definition.Sections[0].Fields[0].Label = "Changed later";

		var frozen = AdmissionEffectiveFormStore.Read(instance)!;
		frozen.Definition.Sections[0].Fields[0].Label.ShouldBe("DOB");
		frozen.InquiryFormDefinitionId.ShouldBe(preview.InquiryFormDefinitionId);
		frozen.Definition.Id.ShouldBe(instance.FormDefinitionId);
		frozen.Definition.Version.ShouldBe(instance.FormVersion);
	}

	[Fact]
	public void Applications_sharing_registration_keep_their_own_inquiry_labels_and_source_identity()
	{
		var first = Instance();
		var second = new AdmissionFormInstance(Guid.NewGuid(), first.TenantId, Guid.NewGuid(), first.FormDefinitionId, first.FormVersion);
		var firstPreview = Preview(first);
		firstPreview.Definition.Sections[0].Fields[0].Label = "Date of birth";
		var secondPreview = Preview(second);
		AdmissionEffectiveFormStore.Freeze(first, firstPreview);
		AdmissionEffectiveFormStore.Freeze(second, secondPreview);

		var firstSchema = AdmissionEffectiveFormStore.Read(first)!;
		var secondSchema = AdmissionEffectiveFormStore.Read(second)!;
		firstSchema.Definition.Id.ShouldBe(secondSchema.Definition.Id);
		firstSchema.InquiryFormDefinitionId.ShouldNotBe(secondSchema.InquiryFormDefinitionId);
		firstSchema.Definition.Sections[0].Fields[0].Label.ShouldBe("Date of birth");
		secondSchema.Definition.Sections[0].Fields[0].Label.ShouldBe("DOB");
	}

	[Fact]
	public void Frozen_schema_cannot_be_replaced_even_while_application_is_draft()
	{
		var instance = Instance();
		AdmissionEffectiveFormStore.Freeze(instance, Preview(instance));

		Should.Throw<BusinessException>(() => AdmissionEffectiveFormStore.Freeze(instance, Preview(instance)))
			.Code.ShouldBe(AdmissionFormInheritanceErrorCodes.SnapshotImmutable);
	}

	[Fact]
	public void Missing_schema_for_enabled_instance_is_not_legacy()
	{
		var instance = Instance();
		instance.ExtraProperties[AdmissionFormInstance.EffectiveSchemaEnabledKey] = true;

		Should.Throw<BusinessException>(() => AdmissionEffectiveFormStore.Read(instance))
			.Code.ShouldBe(AdmissionFormInheritanceErrorCodes.InvalidSnapshot);
	}

	[Theory]
	[InlineData("not-json")]
	[InlineData("{\"formatVersion\":999}")]
	[InlineData("{\"formatVersion\":1,\"definition\":null}")]
	public void Corrupt_enabled_schema_is_rejected(string schema)
	{
		var instance = Instance();
		instance.FreezeEffectiveSchema(schema);

		Should.Throw<BusinessException>(() => AdmissionEffectiveFormStore.Read(instance))
			.Code.ShouldBe(AdmissionFormInheritanceErrorCodes.InvalidSnapshot);
	}

	[Fact]
	public void Schema_cannot_be_frozen_for_another_registration_version()
	{
		var instance = Instance();
		var preview = Preview(instance);
		preview.Definition.Id = Guid.NewGuid();

		Should.Throw<BusinessException>(() => AdmissionEffectiveFormStore.Freeze(instance, preview))
			.Code.ShouldBe(AdmissionFormInheritanceErrorCodes.InvalidSnapshot);
	}

	[Fact]
	public void Missing_explicit_format_version_is_rejected_even_when_definition_is_valid()
	{
		var instance = Instance();
		AdmissionEffectiveFormStore.Freeze(instance, Preview(instance));
		var payload = JsonNode.Parse((string)instance.ExtraProperties[AdmissionFormInstance.EffectiveSchemaKey]!)!.AsObject();
		payload.Remove("formatVersion");
		instance.ExtraProperties[AdmissionFormInstance.EffectiveSchemaKey] = payload.ToJsonString();

		Should.Throw<BusinessException>(() => AdmissionEffectiveFormStore.Read(instance))
			.Code.ShouldBe(AdmissionFormInheritanceErrorCodes.InvalidSnapshot);
	}

	[Fact]
	public void Empty_effective_schema_cannot_be_frozen_as_a_successful_registration()
	{
		var instance = Instance();
		var preview = Preview(instance);
		preview.Definition.Sections.Clear();

		Should.Throw<BusinessException>(() => AdmissionEffectiveFormStore.Freeze(instance, preview))
			.Code.ShouldBe(AdmissionFormInheritanceErrorCodes.InvalidSnapshot);
	}

	[Theory]
	[InlineData("yes", true)]
	[InlineData("no", false)]
	public void Hidden_context_dependencies_are_frozen_without_exposing_source_values(string answer, bool expectedVisible)
	{
		var instance = Instance();
		var preview = Preview(instance);
		var birth = preview.Definition.Sections[0].Fields[0];
		birth.Required = true;
		birth.VisibilityRuleJson = "{\"field\":\"CTX_FLAG\",\"operator\":\"Equals\",\"value\":\"yes\"}";
		preview.InquiryContextSections = [new()
		{
			Id = Guid.NewGuid(), FormDefinitionId = instance.FormDefinitionId,
			Code = "CONTEXT", Name = "Source context", VisibilityRuleJson = "{\"operator\":\"ANY\",\"conditions\":[]}",
			Fields = [new()
			{
				Id = Guid.NewGuid(), FormDefinitionId = instance.FormDefinitionId, Code = "CTX_FLAG", Label = "Internal source detail",
				FieldType = AdmissionFormFieldType.Text, BindingType = AdmissionFormBindingType.System, BindingKey = "Inquiry.VisitPlanned", ReadOnly = true,
				InheritedFrom = new() { InquiryFormDefinitionId = preview.InquiryFormDefinitionId!.Value,
					SourceFieldCode = "SOURCE_FLAG", SourceBindingKey = "Inquiry.VisitPlanned", FieldIdentity = "system:Inquiry.VisitPlanned" }
			}]
		}];

		AdmissionEffectiveFormResolver.PrepareSourceContext(preview, new Dictionary<string, string> { ["Inquiry.VisitPlanned"] = $"\"{answer}\"" });
		AdmissionEffectiveFormStore.Freeze(instance, preview);
		var frozen = AdmissionEffectiveFormStore.Read(instance)!;
		frozen.InquiryContextSections.ShouldBeEmpty();
		frozen.InquiryContextValues.ShouldBeEmpty();
		frozen.Definition.Sections[0].Fields[0].VisibilityRuleJson.ShouldNotBeNull().ShouldNotContain("CTX_FLAG");
		var validator = new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator());
		validator.Validate(AdmissionEffectiveFormResolver.MaterializeFields(frozen.Definition),
			AdmissionEffectiveFormResolver.MaterializeSections(frozen.Definition), new Dictionary<string, string>())
			.FieldErrors.ContainsKey("DOB_EFFECTIVE").ShouldBe(expectedVisible);
	}

	[Fact]
	public void Context_prefill_uses_source_binding_key_and_excludes_unrelated_answers()
	{
		var instance = Instance();
		var preview = Preview(instance);
		preview.InquiryContextSections =
		[
			new()
			{
				Id = Guid.NewGuid(), FormDefinitionId = instance.FormDefinitionId,
				Code = "SOURCE_CONTEXT", Name = "Inquiry context",
				Fields =
				[
					new()
					{
						Id = Guid.NewGuid(), FormDefinitionId = instance.FormDefinitionId,
						Code = "CONTEXT_NOTE", Label = "Inquiry note", FieldType = AdmissionFormFieldType.Text,
						BindingType = AdmissionFormBindingType.System, BindingKey = "Inquiry.Notes", ReadOnly = true,
						InheritedFrom = new()
						{
							InquiryFormDefinitionId = preview.InquiryFormDefinitionId!.Value,
							SourceFieldCode = "SOURCE_NOTE", SourceBindingKey = "Inquiry.Notes", FieldIdentity = "system:Inquiry.Notes"
						}
					}
				]
			}
		];
		AdmissionEffectiveFormStore.Freeze(instance, preview, new Dictionary<string, string>
		{
			["Inquiry.Notes"] = "\"Call after school\"",
			["PRIVATE_UNRELATED"] = "\"Never expose\""
		});

		var frozen = AdmissionEffectiveFormStore.Read(instance)!;
		frozen.InquiryContextValues.ShouldHaveSingleItem();
		frozen.InquiryContextValues[0].FieldCode.ShouldBe("CONTEXT_NOTE");
		frozen.InquiryContextValues[0].ValueJson.ShouldBe("\"Call after school\"");
	}

	[Fact]
	public void Effective_fields_materialize_source_rules_for_existing_validator()
	{
		var instance = Instance();
		var preview = Preview(instance);
		preview.Definition.Sections[0].Fields[0].Required = true;
		preview.Definition.Sections[0].Fields[0].VisibilityRuleJson = "{\"field\":\"SHOW_DOB\",\"operator\":\"Equals\",\"value\":true}";
		AdmissionEffectiveFormStore.Freeze(instance, preview);
		var frozen = AdmissionEffectiveFormStore.Read(instance)!;
		var fields = AdmissionEffectiveFormResolver.MaterializeFields(frozen.Definition);
		var sections = AdmissionEffectiveFormResolver.MaterializeSections(frozen.Definition);
		var validator = new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator());

		validator.Validate(fields, sections, new Dictionary<string, string> { ["SHOW_DOB"] = "false" })
			.Errors.ShouldBeEmpty();
		validator.Validate(fields, sections, new Dictionary<string, string> { ["SHOW_DOB"] = "true" })
			.FieldErrors.ShouldContainKey("DOB_EFFECTIVE");
	}

	private static AdmissionFormInstance Instance() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 4);

	private static AdmissionFormInheritancePreviewDto Preview(AdmissionFormInstance instance)
	{
		var sourceId = Guid.NewGuid();
		var sectionId = Guid.NewGuid();
		return new()
		{
			InquiryFormDefinitionId = sourceId,
			Definition = new()
			{
				Id = instance.FormDefinitionId, Version = instance.FormVersion, Name = "Registration", Code = "REGISTRATION",
				Purpose = AdmissionFormPurpose.Registration, Status = AdmissionFormStatus.Published,
				Sections =
				[
					new()
					{
						Id = sectionId, FormDefinitionId = instance.FormDefinitionId, Code = "STUDENT", Name = "Student details",
						Fields =
						[
							new()
							{
								Id = Guid.NewGuid(), FormDefinitionId = instance.FormDefinitionId, SectionId = sectionId,
								Code = "DOB_EFFECTIVE", Label = "DOB", FieldType = AdmissionFormFieldType.Date,
								BindingType = AdmissionFormBindingType.System, BindingKey = "Applicant.DateOfBirth",
								InheritedFrom = new()
								{
									InquiryFormDefinitionId = sourceId, SourceFieldCode = "BIRTH_DATE",
									SourceBindingKey = "Applicant.DateOfBirth", FieldIdentity = "system:Applicant.DateOfBirth"
								}
							}
						]
					}
				]
			}
		};
	}
}
