using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Resolves one frozen application schema; later form publications never change an existing instance.</summary>
public class AdmissionEffectiveFormResolver(AdmissionFormInheritanceService inheritance,
	IRepository<AdmissionFormDefinition, Guid> definitions) : ITransientDependency
{
	public async Task FreezeAsync(AdmissionApplication application, AdmissionFormInstance instance, AdmissionInquiry? inquiry,
		AdmissionFormDefinition? registration = null)
	{
		registration ??= await definitions.GetAsync(instance.FormDefinitionId);
		if (registration.Id != instance.FormDefinitionId || registration.TenantId != application.TenantId ||
			instance.ApplicationId != application.Id || instance.TenantId != application.TenantId)
			throw new BusinessException(AdmissionFormInheritanceErrorCodes.IncompatibleSource);
		if (AdmissionFormInheritanceMetadataStore.ReadPolicy(registration)?.Enabled != true) return;
		if (inquiry != null && (inquiry.ApplicantId != application.ApplicantId || inquiry.Id != application.InquiryId ||
			inquiry.AdmissionOfferingId != application.AdmissionOfferingId || inquiry.AdmissionCycleId != application.AdmissionCycleId))
			throw new BusinessException(AdmissionFormInheritanceErrorCodes.IncompatibleSource);
		if (inquiry != null && !inquiry.FormResponseDefinitionId.HasValue)
			throw new BusinessException(AdmissionFormInheritanceErrorCodes.IncompatibleSource,
				"The linked Inquiry has no captured form version. Review its form before starting Registration.");
		var preview = await inheritance.ComposeAsync(instance.FormDefinitionId, inquiry?.FormResponseDefinitionId);
		if (!preview.IsValid) throw new BusinessException(AdmissionFormInheritanceErrorCodes.IncompatibleSource)
			.WithData("Errors", string.Join("; ", preview.Issues.Select(issue => issue.Message)));
		var sourceValues = ReadInquiryValues(inquiry?.FormResponseSnapshotJson);
		PrepareSourceContext(preview, sourceValues);
		AdmissionEffectiveFormStore.Freeze(instance, preview, sourceValues);
	}

	internal static void PrepareSourceContext(AdmissionFormInheritancePreviewDto preview, IReadOnlyDictionary<string, string> sourceValues)
	{
		FreezeContextConditions(preview, sourceValues);
		FilterContext(preview, sourceValues);
	}

	public async Task<AdmissionEffectiveFormRuntimeSnapshot> ResolveAsync(AdmissionFormInstance instance)
	{
		var frozen = AdmissionEffectiveFormStore.Read(instance);
		if (frozen != null) return frozen;
		var definition = await inheritance.ReadDefinitionAsync(instance.FormDefinitionId);
		AdmissionEffectiveFormStore.EnsureLegacyAllowed(definition);
		return new() { Definition = definition };
	}

	public static List<AdmissionFormFieldDefinition> MaterializeFields(AdmissionFormDto definition) =>
		definition.Sections.SelectMany(section => section.Fields).Select(field => new AdmissionFormFieldDefinition(
			field.Id, definition.Id, field.SectionId, field.Code, field.Label, field.FieldType, field.BindingType, field.BindingKey,
			field.Required, field.ReadOnly, field.DisplayOrder, field.Placeholder, field.HelpText, field.DefaultValue,
			field.ValidationJson, field.VisibilityRuleJson, field.OptionsJson, field.RequiredRuleJson)).ToList();

	public static List<AdmissionFormSectionDefinition> MaterializeSections(AdmissionFormDto definition) =>
		definition.Sections.Select(section => new AdmissionFormSectionDefinition(section.Id, definition.Id, section.Code, section.Name,
			section.DisplayOrder, CombineRules(section.VisibilityRuleJson, section.StepVisibilityRuleJson),
			section.StepId, section.StepCode, section.StepName, section.StepDisplayOrder, section.StepVisibilityRuleJson)).ToList();

	internal static AdmissionFormExperienceMetadata Experience(AdmissionFormDto definition) => new()
	{
		DefaultLanguage = definition.DefaultLanguage,
		SupportedLanguages = definition.SupportedLanguages,
		Sections = definition.Sections.ToDictionary(section => section.Code, section => new AdmissionFormSectionExperienceMetadata
		{
			IsRepeatable = section.IsRepeatable, MinEntries = section.MinEntries, MaxEntries = section.MaxEntries,
			EntryLabel = section.EntryLabel, Translations = section.Translations,
			Fields = section.Fields.ToDictionary(field => field.Code, field => field.Translations, StringComparer.OrdinalIgnoreCase)
		}, StringComparer.OrdinalIgnoreCase)
	};

	internal static IReadOnlySet<string> VisibleFieldCodes(AdmissionFormDto definition, IReadOnlyDictionary<string, string> values,
		AdmissionApplicationFormValidator validator)
	{
		var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var fields = MaterializeFields(definition);
		foreach (var section in MaterializeSections(definition))
		{
			var sectionFields = fields.Where(field => field.SectionId == section.Id).ToList();
			var schema = definition.Sections.Single(item => item.Id == section.Id);
			if (!schema.IsRepeatable)
			{
				visible.UnionWith(validator.GetVisibleFieldCodes(sectionFields, [section], values));
				continue;
			}
			var rows = sectionFields.ToDictionary(field => field.Code, field => ReadRepeatedValues(values.GetValueOrDefault(field.Code)), StringComparer.OrdinalIgnoreCase);
			var count = Math.Max(1, rows.Values.Select(items => items.Count).DefaultIfEmpty(0).Max());
			for (var index = 0; index < count; index++)
			{
				var rowValues = values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
				foreach (var field in sectionFields) rowValues[field.Code] = rows[field.Code].ElementAtOrDefault(index) ?? string.Empty;
				visible.UnionWith(validator.GetVisibleFieldCodes(sectionFields, [section], rowValues));
			}
		}
		return visible;
	}

	internal static IReadOnlyDictionary<string, string> ReadInquiryValues(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			return new Dictionary<string, string>(JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [], StringComparer.OrdinalIgnoreCase);
		}
		catch (JsonException)
		{
			throw new BusinessException(AdmissionFormInheritanceErrorCodes.InvalidSnapshot,
				"The captured Inquiry answers cannot be read. Review the Inquiry before starting Registration.");
		}
	}

	private static void FilterContext(AdmissionFormInheritancePreviewDto preview, IReadOnlyDictionary<string, string> sourceValues)
	{
		var context = preview.InquiryContextSections;
		if (context.Count == 0) return;
		var values = preview.Definition.Sections.Concat(context).SelectMany(section => section.Fields)
			.Where(field => field.InheritedFrom != null).ToDictionary(field => field.Code,
				field => AdmissionApplicationValueConverter.Unwrap(AdmissionEffectiveFormStore.ReadInquiryValue(field, sourceValues)), StringComparer.OrdinalIgnoreCase);
		var evaluator = new AdmissionWorkflowConditionEvaluator();
		foreach (var section in context)
		{
			if (!evaluator.Evaluate(section.VisibilityRuleJson, values) || !evaluator.Evaluate(section.StepVisibilityRuleJson, values))
				section.Fields = [];
			else section.Fields = section.Fields.Where(field => evaluator.Evaluate(field.VisibilityRuleJson, values)).ToList();
			// Source context is historical information. Its visibility was evaluated against the captured answers, not later edits.
			section.VisibilityRuleJson = null;
			section.StepVisibilityRuleJson = null;
			foreach (var field in section.Fields) field.VisibilityRuleJson = null;
		}
		preview.InquiryContextSections = context.Where(section => section.Fields.Count > 0).ToList();
	}

	private static void FreezeContextConditions(AdmissionFormInheritancePreviewDto preview, IReadOnlyDictionary<string, string> sourceValues)
	{
		var contextFields = preview.InquiryContextSections.SelectMany(section => section.Fields).ToList();
		if (contextFields.Count == 0) return;
		var contextCodes = contextFields.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
		var values = preview.Definition.Sections.Concat(preview.InquiryContextSections).SelectMany(section => section.Fields)
			.Where(field => field.InheritedFrom != null).ToDictionary(field => field.Code,
				field => AdmissionApplicationValueConverter.Unwrap(AdmissionEffectiveFormStore.ReadInquiryValue(field, sourceValues)), StringComparer.OrdinalIgnoreCase);
		foreach (var section in preview.Definition.Sections)
		{
			section.VisibilityRuleJson = FreezeRule(section.VisibilityRuleJson, contextCodes, values);
			section.StepVisibilityRuleJson = FreezeRule(section.StepVisibilityRuleJson, contextCodes, values);
			foreach (var field in section.Fields)
			{
				field.VisibilityRuleJson = FreezeRule(field.VisibilityRuleJson, contextCodes, values);
				field.RequiredRuleJson = FreezeRule(field.RequiredRuleJson, contextCodes, values);
			}
		}
	}

	private static string? FreezeRule(string? json, IReadOnlySet<string> contextCodes, IReadOnlyDictionary<string, string> values)
	{
		if (string.IsNullOrWhiteSpace(json)) return json;
		var rule = JsonNode.Parse(json);
		var evaluator = new AdmissionWorkflowConditionEvaluator();
		return Freeze(rule)?.ToJsonString();

		JsonNode? Freeze(JsonNode? node)
		{
			if (node is not JsonObject condition) return node;
			if (condition["field"] is JsonValue fieldNode && fieldNode.TryGetValue<string>(out var code) && contextCodes.Contains(code))
				return new JsonObject
				{
					["operator"] = evaluator.Evaluate(condition.ToJsonString(), values) ? "ALL" : "ANY",
					["conditions"] = new JsonArray()
				};
			if (condition["conditions"] is JsonArray children)
				for (var index = 0; index < children.Count; index++) children[index] = Freeze(children[index]?.DeepClone());
			return condition;
		}
	}

	private static IReadOnlyList<string> ReadRepeatedValues(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return [];
		try
		{
			using var parsed = JsonDocument.Parse(json);
			return parsed.RootElement.ValueKind == JsonValueKind.Array
				? parsed.RootElement.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText()).ToList()
				: [];
		}
		catch (JsonException) { return []; }
	}

	private static string? CombineRules(string? section, string? step)
	{
		if (string.IsNullOrWhiteSpace(section)) return step;
		if (string.IsNullOrWhiteSpace(step)) return section;
		return new JsonObject
		{
			["operator"] = "ALL",
			["conditions"] = new JsonArray(JsonNode.Parse(section), JsonNode.Parse(step))
		}.ToJsonString();
	}
}
