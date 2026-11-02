using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionGenderChoices
{
	internal static void NormalizeDraftDefinition(CreateUpdateAdmissionFormDto input)
	{
		var repairedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var field in input.Sections.SelectMany(section => section.Fields))
		{
			var options = NormalizeDraftOptions(field);
			if (options == field.OptionsJson) continue;
			repairedCodes.Add(field.Code);
			field.OptionsJson = options;
			if (field.DefaultValue == "OPTION_3") field.DefaultValue = "Other";
			NormalizeTranslations(field.Translations);
		}
		if (repairedCodes.Count == 0) return;
		foreach (var section in input.Sections)
		{
			section.VisibilityRuleJson = NormalizeRule(section.VisibilityRuleJson, repairedCodes);
			section.StepVisibilityRuleJson = NormalizeRule(section.StepVisibilityRuleJson, repairedCodes);
			foreach (var field in section.Fields)
			{
				field.VisibilityRuleJson = NormalizeRule(field.VisibilityRuleJson, repairedCodes);
				field.RequiredRuleJson = NormalizeRule(field.RequiredRuleJson, repairedCodes);
			}
		}
	}

	internal static void NormalizeTranslations(Dictionary<string, AdmissionFormTranslationDto> translations)
	{
		foreach (var translation in translations.Values)
			if (translation.Choices.Remove("OPTION_3", out var label)) translation.Choices.TryAdd("Other", label);
	}

	internal static string? NormalizeRule(string? json, ISet<string> repairedCodes)
	{
		if (string.IsNullOrWhiteSpace(json)) return json;
		try
		{
			var node = JsonNode.Parse(json);
			return Rewrite(node) ? node!.ToJsonString() : json;
		}
		catch (Exception exception) when (exception is JsonException or InvalidOperationException) { return json; }

		bool Rewrite(JsonNode? node)
		{
			if (node is not JsonObject rule) return false;
			var changed = false;
			if (rule["conditions"] is JsonArray conditions)
				foreach (var condition in conditions) changed |= Rewrite(condition);
			if (rule["field"] is JsonValue field && field.TryGetValue<string>(out var code) && repairedCodes.Contains(code))
			{
				if (rule["value"] is JsonValue value && value.TryGetValue<string>(out var text) && text == "OPTION_3")
				{
					rule["value"] = "Other";
					changed = true;
				}
				else if (rule["value"] is JsonValue listValue && listValue.TryGetValue<string>(out var list) &&
					rule["operator"] is JsonValue operation && operation.TryGetValue<string>(out var name) &&
					(string.Equals(name, "In", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "NotIn", StringComparison.OrdinalIgnoreCase)))
				{
					var mapped = string.Join(",", list.Split(',').Select(item => item.Trim() == "OPTION_3" ? "Other" : item));
					if (mapped != list) { rule["value"] = mapped; changed = true; }
				}
				else if (rule["value"] is JsonArray values)
					for (var index = 0; index < values.Count; index++)
						if (values[index] is JsonValue item && item.TryGetValue<string>(out var choice) && choice == "OPTION_3")
						{
							values[index] = "Other";
							changed = true;
						}
			}
			return changed;
		}
	}

	internal static string? NormalizeDraftOptions(CreateUpdateAdmissionFormFieldDto field) =>
		NormalizeDraftOptions(field.BindingType, field.BindingKey, field.FieldType, field.OptionsJson);

	internal static string? NormalizeDraftOptions(AdmissionFormBindingType type, string? key, AdmissionFormFieldType fieldType, string? json)
	{
		// Repair only the known unpublished generic-editor shape. Labels never define
		// arbitrary gender values, and runtime/published snapshots never call this.
		if (type != AdmissionFormBindingType.System || key != "Applicant.Gender" ||
			fieldType is not (AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio) || string.IsNullOrWhiteSpace(json)) return json;
		try
		{
			if (JsonNode.Parse(json) is not JsonArray { Count: 3 } options || options.Any(option => option is not JsonObject)) return json;
			var values = options.Select(option => option!["value"]?.GetValue<string>()).ToArray();
			if (!values.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(new[] { "Female", "Male", "OPTION_3" })) return json;
			var other = options.Single(option => option!["value"]!.GetValue<string>() == "OPTION_3")!;
			if (other["label"]?.GetValue<string>() != "Other") return json;
			other["value"] = "Other";
			return options.ToJsonString();
		}
		catch (Exception exception) when (exception is JsonException or InvalidOperationException) { return json; }
	}

	internal static AdmissionFormFieldDefinition ForValidation(AdmissionFormFieldDefinition field, AdmissionFormStatus status)
	{
		if (status != AdmissionFormStatus.Draft) return field;
		var options = NormalizeDraftOptions(field.BindingType, field.BindingKey, field.FieldType, field.OptionsJson);
		return options == field.OptionsJson ? field : new AdmissionFormFieldDefinition(field.Id, field.FormDefinitionId, field.SectionId,
			field.Code, field.Label, field.FieldType, field.BindingType, field.BindingKey, field.Required, field.ReadOnly,
			field.DisplayOrder, field.Placeholder, field.HelpText, field.DefaultValue, field.ValidationJson,
			field.VisibilityRuleJson, options, field.RequiredRuleJson);
	}
}
