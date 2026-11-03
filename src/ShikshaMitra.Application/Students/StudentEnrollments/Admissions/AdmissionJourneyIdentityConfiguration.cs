using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionJourneyIdentityConfiguration
{
	internal static readonly string[] CoreBindings = ["Applicant.FirstName", "Applicant.DateOfBirth", "Applicant.Gender"];
	// AdmissionDirectRegistrationService requires these fixed-entry answers before
	// creating the applicant. Surname remains an optional fixed-entry answer.
	internal static readonly string[] DirectEntryCapturedBindings = ["Applicant.FirstName", "Applicant.DateOfBirth", "Applicant.Gender"];

	internal static IReadOnlyList<string> MissingBindings(AdmissionFormSchema registration, AdmissionFormSchema? inquiry,
		IReadOnlyCollection<string>? guaranteedCapturedBindings = null)
	{
		// A full-name question stores the name without splitting it. It must not
		// compete with writable name parts or clear a required upstream name with
		// an optional/unreachable Registration answer.
		var fields = registration.Sections.SelectMany(section => section.Fields).ToArray();
		var rewritesName = fields.Any(field =>
			!field.ReadOnly && field.BindingType == AdmissionFormBindingType.System && field.BindingKey == "Applicant.Name");
		var unsafeName = rewritesName && (fields.Any(field => !field.ReadOnly && field.BindingType == AdmissionFormBindingType.System &&
			field.BindingKey is "Applicant.FirstName" or "Applicant.MiddleName" or "Applicant.LastName") || !Supplies(registration, "Applicant.Name"));
		return CoreBindings.Where(key => unsafeName && key == "Applicant.FirstName" ||
			guaranteedCapturedBindings?.Contains(key, StringComparer.Ordinal) != true &&
			!SuppliesIdentity(registration, key) && (inquiry == null || !SuppliesIdentity(inquiry, key))).ToArray();
	}

	private static bool SuppliesIdentity(AdmissionFormSchema form, string key) => Supplies(form, key) ||
		key == "Applicant.FirstName" && Supplies(form, "Applicant.Name");

	private static bool Supplies(AdmissionFormSchema form, string key) => form.Sections.Any(section =>
		!section.IsRepeatable && AlwaysTrue(section.VisibilityRuleJson) && AlwaysTrue(section.StepVisibilityRuleJson) &&
		section.Fields.Any(field => field.BindingType == AdmissionFormBindingType.System &&
			string.Equals(field.BindingKey, key, StringComparison.Ordinal) && !field.ReadOnly &&
			(field.Required || !string.IsNullOrWhiteSpace(field.RequiredRuleJson) && AlwaysTrue(field.RequiredRuleJson)) &&
			AlwaysTrue(field.VisibilityRuleJson) && CompatibleType(field, key)));

	private static bool CompatibleType(AdmissionFormSchemaField field, string key) => key switch
	{
		"Applicant.DateOfBirth" => field.FieldType == AdmissionFormFieldType.Date,
		"Applicant.Gender" => field.FieldType is AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio && SupportedGenderChoices(field.OptionsJson),
		_ => field.FieldType == AdmissionFormFieldType.Text
	};

	private static bool SupportedGenderChoices(string? json)
	{
		// An absent override uses the binding registry's canonical gender choices.
		if (string.IsNullOrWhiteSpace(json)) return true;
		try
		{
			using var parsed = JsonDocument.Parse(json);
			return parsed.RootElement.ValueKind == JsonValueKind.Array && parsed.RootElement.GetArrayLength() > 0 &&
				parsed.RootElement.EnumerateArray().All(SupportedGenderChoice);
		}
		catch (JsonException) { return false; }
	}

	private static bool SupportedGenderChoice(JsonElement option)
	{
		var value = option.ValueKind == JsonValueKind.String ? option.GetString() :
			option.ValueKind == JsonValueKind.Object && option.TryGetProperty("value", out var property) && property.ValueKind == JsonValueKind.String
				? property.GetString() : null;
		return string.Equals(value, "Male", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "Female", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(value, "Other", StringComparison.OrdinalIgnoreCase);
	}

	private static bool AlwaysTrue(string? json)
	{
		if (string.IsNullOrWhiteSpace(json)) return true;
		try
		{
			using var parsed = JsonDocument.Parse(json);
			return ConstantTrue(parsed.RootElement);
		}
		catch (JsonException) { return false; }
	}

	private static bool ConstantTrue(JsonElement node)
	{
		// Conditions on unanswered identity or other optional answers cannot guarantee
		// a route to a core value. Only unconditional groups count as guaranteed here.
		if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("operator", out var operation) ||
			operation.ValueKind != JsonValueKind.String || !node.TryGetProperty("conditions", out var conditions) ||
			conditions.ValueKind != JsonValueKind.Array) return false;
		return operation.GetString()!.Equals("ALL", StringComparison.OrdinalIgnoreCase)
			? conditions.EnumerateArray().All(ConstantTrue)
			: operation.GetString()!.Equals("ANY", StringComparison.OrdinalIgnoreCase) && conditions.EnumerateArray().Any(ConstantTrue);
	}
}
