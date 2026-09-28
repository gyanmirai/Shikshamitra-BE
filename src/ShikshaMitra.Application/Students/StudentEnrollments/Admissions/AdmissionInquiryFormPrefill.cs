using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionInquiryFormPrefill
{
	public static string? ResolveCustomValue(
		AdmissionFormFieldDefinition target,
		IReadOnlyCollection<AdmissionFormFieldDefinition> inquiryFields,
		IReadOnlyDictionary<string, string> inquiryValues)
	{
		if (target.BindingType != AdmissionFormBindingType.Custom) return null;
		var key = Key(target);
		var matches = inquiryFields.Where(field =>
			field.BindingType == AdmissionFormBindingType.Custom &&
			string.Equals(Key(field), key, StringComparison.OrdinalIgnoreCase)).ToList();
		if (matches.Count != 1 || !inquiryValues.TryGetValue(key, out var value) ||
			string.IsNullOrWhiteSpace(value) || value == "null") return null;

		var source = matches[0];
		var sameQuestion = NormalizeLabel(source.Label) == NormalizeLabel(target.Label);
		var deliberateSharedBinding = !string.Equals(source.BindingKey, source.Code, StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(target.BindingKey, target.Code, StringComparison.OrdinalIgnoreCase);
		if (!sameQuestion && !deliberateSharedBinding) return null;
		if (source.FieldType == target.FieldType) return ChoiceAllows(target, value) ? value : null;
		var sourceIsTextOrChoice = source.FieldType is AdmissionFormFieldType.Text or
			AdmissionFormFieldType.Textarea or AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio;
		var targetIsTextOrChoice = target.FieldType is AdmissionFormFieldType.Text or
			AdmissionFormFieldType.Textarea or AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio;
		return sourceIsTextOrChoice && targetIsTextOrChoice && ChoiceAllows(target, value) ? value : null;
	}

	private static string Key(AdmissionFormFieldDefinition field) =>
		string.IsNullOrWhiteSpace(field.BindingKey) ? field.Code : field.BindingKey;

	private static string NormalizeLabel(string label) =>
		new(label.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

	private static bool ChoiceAllows(AdmissionFormFieldDefinition target, string value)
	{
		if (target.FieldType is not (AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio))
			return true;
		if (string.IsNullOrWhiteSpace(target.OptionsJson)) return false;
		try
		{
			using var selected = JsonDocument.Parse(value);
			using var options = JsonDocument.Parse(target.OptionsJson);
			if (selected.RootElement.ValueKind != JsonValueKind.String ||
				options.RootElement.ValueKind != JsonValueKind.Array) return false;
			var selectedValue = selected.RootElement.GetString();
			return options.RootElement.EnumerateArray().Any(option =>
				option.ValueKind == JsonValueKind.String
					? string.Equals(option.GetString(), selectedValue, StringComparison.Ordinal)
					: option.ValueKind == JsonValueKind.Object &&
					  option.TryGetProperty("value", out var optionValue) &&
					  string.Equals(optionValue.ToString(), selectedValue, StringComparison.Ordinal));
		}
		catch (JsonException)
		{
			return false;
		}
	}
}
