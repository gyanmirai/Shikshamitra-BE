using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionDocumentCondition
{
	private static readonly HashSet<string> Operators = new(StringComparer.OrdinalIgnoreCase)
	{
		"Equals", "NotEquals", "Contains", "NotContains", "StartsWith", "EndsWith", "IsEmpty", "IsNotEmpty",
		"GreaterThan", "GreaterThanOrEqual", "LessThan", "LessThanOrEqual", "In", "NotIn"
	};
	public static void Validate(string? json) => GetFields(json);
	public static IReadOnlyList<string> GetFields(string? json)
	{
		if (string.IsNullOrWhiteSpace(json) || json.Length > 8192) throw new ArgumentException("Provide a valid document condition (8192 characters maximum).");
		using var document = JsonDocument.Parse(json);
		var fields = new List<string>();
		Read(document.RootElement, fields, 0);
		return fields;
	}
	private static void Read(JsonElement node, List<string> fields, int depth)
	{
		if (depth > 5 || fields.Count >= 30 || node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("operator", out var operation) || operation.ValueKind != JsonValueKind.String)
			throw new ArgumentException("Use a supported document condition with at most 30 fields and 5 nested groups.");
		var op = operation.GetString()!;
		if (node.TryGetProperty("conditions", out var children))
		{
			if (!op.Equals("ALL", StringComparison.OrdinalIgnoreCase) && !op.Equals("ANY", StringComparison.OrdinalIgnoreCase) || children.ValueKind != JsonValueKind.Array || children.GetArrayLength() is < 1 or > 30)
				throw new ArgumentException("Document condition groups must use ALL or ANY.");
			foreach (var child in children.EnumerateArray()) Read(child, fields, depth + 1);
			return;
		}
		if (!Operators.Contains(op) || !node.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.String)
			throw new ArgumentException("Use a supported document condition field and operator.");
		var name = field.GetString()!;
		if (name.Length > 128 || !new[] { "application.", "inquiry.", "assessment." }.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.Length > prefix.Length))
			throw new ArgumentException("Document conditions may reference application, inquiry, or assessment fields.");
		if (!op.Equals("IsEmpty", StringComparison.OrdinalIgnoreCase) && !op.Equals("IsNotEmpty", StringComparison.OrdinalIgnoreCase))
		{
			if (!node.TryGetProperty("value", out var value)) throw new ArgumentException("This document condition needs a comparison value.");
			ValidateValue(op, value);
		}
		fields.Add(name);
	}
	private static void ValidateValue(string operation, JsonElement value)
	{
		var collection = operation.Equals("In", StringComparison.OrdinalIgnoreCase) || operation.Equals("NotIn", StringComparison.OrdinalIgnoreCase);
		if (collection && value.ValueKind == JsonValueKind.Array)
		{
			if (value.GetArrayLength() is < 1 or > 30) throw new ArgumentException("Condition choices allow 1 to 30 values.");
			foreach (var item in value.EnumerateArray()) ValidateScalar(item);
			return;
		}
		ValidateScalar(value);
		if (operation.StartsWith("GreaterThan", StringComparison.OrdinalIgnoreCase) || operation.StartsWith("LessThan", StringComparison.OrdinalIgnoreCase))
			if (!decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out _))
				throw new ArgumentException("Numeric document comparisons require a valid number.");
	}
	private static void ValidateScalar(JsonElement value)
	{
		if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) || value.ToString().Length > 1024)
			throw new ArgumentException("Document condition values must be text, numbers, or booleans up to 1024 characters.");
	}
}
