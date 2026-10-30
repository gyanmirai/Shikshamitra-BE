using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>The immutable schema and safe source context belonging to one application, not a shared form definition.</summary>
public sealed class AdmissionEffectiveFormRuntimeSnapshot
{
	public int FormatVersion { get; set; } = 1;
	public AdmissionFormDto Definition { get; set; } = new();
	public Guid? InquiryFormDefinitionId { get; set; }
	public List<AdmissionFormSectionDto> InquiryContextSections { get; set; } = [];
	public List<AdmissionFormValueDto> InquiryContextValues { get; set; } = [];
}

public static class AdmissionEffectiveFormStore
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static void Freeze(AdmissionFormInstance instance, AdmissionFormInheritancePreviewDto preview,
		IReadOnlyDictionary<string, string>? inquiryValues = null)
	{
		if (!preview.IsValid) throw new BusinessException(AdmissionFormInheritanceErrorCodes.IncompatibleSource)
			.WithData("Errors", string.Join("; ", preview.Issues.Select(issue => issue.Message)));
		var snapshot = new AdmissionEffectiveFormRuntimeSnapshot
		{
			Definition = preview.Definition,
			InquiryFormDefinitionId = preview.InquiryFormDefinitionId,
			InquiryContextSections = preview.InquiryContextSections,
			InquiryContextValues = preview.InquiryContextSections.SelectMany(section => section.Fields)
				.Where(field => field.InheritedFrom != null)
				.Select(field => new AdmissionFormValueDto
				{
					FieldCode = field.Code,
					ValueJson = ReadInquiryValue(field, inquiryValues)
				}).Where(value => value.ValueJson != null).ToList()
		};
		Validate(instance, snapshot);
		instance.FreezeEffectiveSchema(JsonSerializer.Serialize(snapshot, JsonOptions));
	}

	public static AdmissionEffectiveFormRuntimeSnapshot? Read(AdmissionFormInstance instance)
	{
		if (!instance.ExtraProperties.ContainsKey(AdmissionFormInstance.EffectiveSchemaKey) &&
			!instance.ExtraProperties.ContainsKey(AdmissionFormInstance.EffectiveSchemaEnabledKey)) return null;
		try
		{
			var enabled = instance.GetProperty<bool>(AdmissionFormInstance.EffectiveSchemaEnabledKey);
			var json = instance.GetProperty<string>(AdmissionFormInstance.EffectiveSchemaKey);
			if (!enabled || string.IsNullOrWhiteSpace(json)) throw InvalidSnapshot();
			using var document = JsonDocument.Parse(json);
			if (!document.RootElement.TryGetProperty("formatVersion", out var formatVersion) ||
				formatVersion.ValueKind != JsonValueKind.Number || !formatVersion.TryGetInt32(out var version) || version != 1)
				throw InvalidSnapshot();
			var snapshot = JsonSerializer.Deserialize<AdmissionEffectiveFormRuntimeSnapshot>(json, JsonOptions) ?? throw InvalidSnapshot();
			Validate(instance, snapshot);
			return snapshot;
		}
		catch (Exception exception) when (exception is JsonException or InvalidCastException or ArgumentException or NullReferenceException or InvalidOperationException or FormatException)
		{
			throw InvalidSnapshot();
		}
	}

	public static void EnsureLegacyAllowed(AdmissionFormDefinition definition)
	{
		if (AdmissionFormInheritanceMetadataStore.ReadPolicy(definition)?.Enabled == true) throw InvalidSnapshot();
	}

	public static void EnsureLegacyAllowed(AdmissionFormDto definition)
	{
		if (definition.Inheritance?.Enabled == true) throw InvalidSnapshot();
	}

	public static string? ReadInquiryValue(AdmissionFormFieldDto field, IReadOnlyDictionary<string, string>? inquiryValues)
	{
		if (field.InheritedFrom == null || inquiryValues == null) return null;
		var sourceKey = string.IsNullOrWhiteSpace(field.InheritedFrom.SourceBindingKey)
			? field.InheritedFrom.SourceFieldCode : field.InheritedFrom.SourceBindingKey;
		return inquiryValues.TryGetValue(sourceKey, out var value) ? value : null;
	}

	private static void Validate(AdmissionFormInstance instance, AdmissionEffectiveFormRuntimeSnapshot snapshot)
	{
		if (snapshot.FormatVersion != 1 || snapshot.Definition == null ||
			snapshot.Definition.Id != instance.FormDefinitionId || snapshot.Definition.Version != instance.FormVersion ||
			snapshot.Definition.Purpose != AdmissionFormPurpose.Registration || snapshot.Definition.Sections == null || snapshot.Definition.Sections.Count == 0 ||
			snapshot.InquiryContextSections == null || snapshot.InquiryContextValues == null) throw InvalidSnapshot();
		var sections = snapshot.Definition.Sections;
		if (sections.Any(section => section.Fields == null || section.Id == Guid.Empty || string.IsNullOrWhiteSpace(section.Code)) ||
			sections.GroupBy(section => section.Id).Any(group => group.Count() != 1) ||
			sections.GroupBy(section => section.Code, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() != 1)) throw InvalidSnapshot();
		var fields = sections.SelectMany(section => section.Fields).ToList();
		if (fields.Any(field => string.IsNullOrWhiteSpace(field.Code) || field.FormDefinitionId != instance.FormDefinitionId ||
			!sections.Any(section => section.Id == field.SectionId) ||
			(field.InheritedFrom != null && field.InheritedFrom.InquiryFormDefinitionId != snapshot.InquiryFormDefinitionId)) ||
			fields.GroupBy(field => field.Code, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() != 1)) throw InvalidSnapshot();
		if (snapshot.InquiryContextSections.Any(section => section.Fields == null || section.Fields.Any(field => !field.ReadOnly ||
			field.InheritedFrom == null || field.InheritedFrom.InquiryFormDefinitionId != snapshot.InquiryFormDefinitionId))) throw InvalidSnapshot();
	}

	private static BusinessException InvalidSnapshot() => new(AdmissionFormInheritanceErrorCodes.InvalidSnapshot,
		"The Registration form configuration saved for this application is unavailable. Contact the admission administrator.");
}
