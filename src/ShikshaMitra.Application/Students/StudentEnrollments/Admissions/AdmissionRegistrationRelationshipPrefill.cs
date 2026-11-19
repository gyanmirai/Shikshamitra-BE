using System;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Bridges the legacy Registration relationship question without changing contact ownership.</summary>
internal static class AdmissionRegistrationRelationshipPrefill
{
	internal static bool CanFill(AdmissionFormFieldDto field) => !field.ReadOnly &&
		field.BindingType == AdmissionFormBindingType.System &&
		string.Equals(field.BindingKey, "Guardian.Relationship", StringComparison.OrdinalIgnoreCase) &&
		field.FieldType is AdmissionFormFieldType.Text or AdmissionFormFieldType.Textarea or AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio;

	internal static string? Resolve(AdmissionFormFieldDto field, AdmissionApplicant applicant)
	{
		if (!CanFill(field)) return null;
		var saved = AdmissionAdditionalStudentFieldCatalog.GetValue("Guardian.Relationship", applicant)?.ToString();
		if (!string.IsNullOrWhiteSpace(saved)) return saved;
		var role = AdmissionPrimaryContactSelection.ReadRelationship(applicant);
		if (!AdmissionPrimaryContactSelection.IsRelationship(role)) return null;
		if (field.FieldType is AdmissionFormFieldType.Text or AdmissionFormFieldType.Textarea)
			return role switch { "father" => "Father", "mother" => "Mother", _ => "Guardian" };
		try
		{
			using var options = JsonDocument.Parse(field.OptionsJson ?? "[]");
			if (options.RootElement.ValueKind != JsonValueKind.Array) return null;
			foreach (var option in options.RootElement.EnumerateArray())
			{
				if (option.ValueKind != JsonValueKind.Object || !option.TryGetProperty("value", out var value) ||
					value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)) continue;
				if (string.Equals(value.ToString(), role, StringComparison.OrdinalIgnoreCase) ||
					option.TryGetProperty("label", out var label) && label.ValueKind == JsonValueKind.String &&
					string.Equals(label.GetString(), role, StringComparison.OrdinalIgnoreCase))
					return value.ToString();
			}
		}
		catch (JsonException) { /* Invalid or dynamic choices are never guessed. */ }
		return null;
	}

	internal static void Project(AdmissionFormDto definition, AdmissionApplicant applicant,
		AdmissionApplicationFormDto result, AdmissionApplicationFormValidator validator)
	{
		var candidates = definition.Sections.Where(section => !section.IsRepeatable).SelectMany(section => section.Fields)
			// An inherited null clear can have no stored row. Its exact Inquiry source remains authoritative.
			.Where(field => CanFill(field) && field.InheritedFrom == null &&
				!result.Values.Any(value => string.Equals(value.FieldCode, field.Code, StringComparison.OrdinalIgnoreCase)))
			.Select(field => (Field: field, Value: Resolve(field, applicant))).Where(item => item.Value != null).ToList();
		if (candidates.Count == 0) return;
		var answers = result.Values.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
		foreach (var item in candidates) answers[item.Field.Code] = item.Value!;
		var visible = AdmissionEffectiveFormResolver.VisibleFieldCodes(definition, answers, validator);
		foreach (var item in candidates.Where(item => visible.Contains(item.Field.Code)))
		{
			result.Values.Add(new() { FieldCode = item.Field.Code, ValueJson = JsonSerializer.Serialize(item.Value) });
			result.AutoFilledFieldCodes.Add(item.Field.Code);
		}
	}
}
