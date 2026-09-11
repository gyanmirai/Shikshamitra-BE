using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionEligibilityConditionSchema
{
	public static void Validate(string json, IReadOnlyDictionary<string, AdmissionFormFieldType> fields)
	{
		AdmissionDocumentCondition.Validate(json);
		using var document = JsonDocument.Parse(json);
		Read(document.RootElement, fields);
	}
	private static void Read(JsonElement node, IReadOnlyDictionary<string, AdmissionFormFieldType> fields)
	{
		if (node.TryGetProperty("conditions", out var children))
		{
			foreach (var child in children.EnumerateArray()) Read(child, fields);
			return;
		}
		var field = node.GetProperty("field").GetString()!;
		if (new[] { "application.status", "inquiry.status", "inquiry.source" }.Contains(field, StringComparer.OrdinalIgnoreCase))
			throw new ArgumentException("Reserved lifecycle facts cannot be supplied by answer fields.");
		if (!fields.TryGetValue(field, out var type) || type is AdmissionFormFieldType.Date or AdmissionFormFieldType.File or AdmissionFormFieldType.AddressGroup or AdmissionFormFieldType.Static)
			throw new ArgumentException($"'{field}' must reference a supported field in the selected form.");
		var operation = node.GetProperty("operator").GetString()!.ToLowerInvariant();
		if (operation is "isempty" or "isnotempty") return;
		var permitted = type switch
		{
			AdmissionFormFieldType.Number => new[] { "equals", "notequals", "greaterthan", "greaterthanorequal", "lessthan", "lessthanorequal", "in", "notin" },
			AdmissionFormFieldType.Checkbox => ["equals", "notequals"],
			AdmissionFormFieldType.Select or AdmissionFormFieldType.Radio => ["equals", "notequals", "in", "notin"],
			AdmissionFormFieldType.MultiSelect => ["equals", "notequals", "in", "notin", "contains", "notcontains"],
			_ => ["equals", "notequals", "contains", "notcontains", "startswith", "endswith", "in", "notin"]
		};
		if (!permitted.Contains(operation)) throw new ArgumentException($"'{field}' does not support this comparison operator.");
		var value = node.GetProperty("value");
		var operands = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(item => item.ToString()).ToList() :
			operation is "in" or "notin" ? value.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList() : [value.ToString()];
		if ((operation is "in" or "notin") && (operands.Count is < 1 or > 30)) throw new ArgumentException("Choose between 1 and 30 comparison values.");
		if (type == AdmissionFormFieldType.Number && operands.Any(item => !decimal.TryParse(item, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
			throw new ArgumentException($"'{field}' requires numeric comparison values.");
		if (type == AdmissionFormFieldType.Checkbox && operands.Any(item => !bool.TryParse(item, out _)))
			throw new ArgumentException($"'{field}' requires true or false.");
	}
}
