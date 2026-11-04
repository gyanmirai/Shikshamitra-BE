using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Volo.Abp;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Public-only effective source; published staff definitions are never changed.</summary>
internal static class AdmissionPublicInquirySchema
{
	private const string SnapshotKey = "AdmissionPublicInquiryEffectiveSource";
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AdmissionFormDto Project(AdmissionFormDto source)
	{
		var result = JsonSerializer.Deserialize<AdmissionFormDto>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions)!;
		var omitted = result.Sections.SelectMany(section => section.Fields)
			.Where(field => OmittedValue(field) != null).ToDictionary(field => field.Code, field => OmittedValue(field)!, StringComparer.Ordinal);
		foreach (var section in result.Sections)
		{
			section.VisibilityRuleJson = ProjectRule(section.VisibilityRuleJson, omitted);
			section.StepVisibilityRuleJson = ProjectRule(section.StepVisibilityRuleJson, omitted);
			section.Fields = section.Fields.Where(field => !omitted.ContainsKey(field.Code)).OrderBy(field => field.DisplayOrder).ToList();
			foreach (var field in section.Fields)
			{
				field.VisibilityRuleJson = ProjectRule(field.VisibilityRuleJson, omitted);
				field.RequiredRuleJson = ProjectRule(field.RequiredRuleJson, omitted);
			}
		}
		result.Sections = result.Sections.Where(section => section.Fields.Count > 0).OrderBy(section => section.DisplayOrder).ToList();
		result.SectionCount = result.Sections.Count;
		return result;
	}

	public static void Capture(AdmissionInquiry inquiry, AdmissionFormDto definition) => inquiry.SetProperty(SnapshotKey,
		JsonSerializer.Serialize(new CapturedSource { TenantId = inquiry.TenantId, Definition = definition }, JsonOptions));

	public static AdmissionFormDto? ReadCaptured(AdmissionInquiry inquiry)
	{
		if (!inquiry.ExtraProperties.ContainsKey(SnapshotKey)) return null;
		try
		{
			var json = inquiry.GetProperty<string>(SnapshotKey);
			using var document = JsonDocument.Parse(json!);
			if (!document.RootElement.TryGetProperty("formatVersion", out var version) || !version.TryGetInt32(out var number) || number != 1)
				throw InvalidSnapshot();
			var snapshot = JsonSerializer.Deserialize<CapturedSource>(json!, JsonOptions);
			if (snapshot?.Definition == null || snapshot.TenantId != inquiry.TenantId ||
				snapshot.Definition.Id != inquiry.FormResponseDefinitionId || snapshot.Definition.Purpose != AdmissionFormPurpose.Inquiry)
				throw InvalidSnapshot();
			return snapshot.Definition;
		}
		catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or InvalidCastException)
		{
			throw InvalidSnapshot();
		}
	}

	private static string? OmittedValue(AdmissionFormFieldDto field)
	{
		var binding = field.BindingKey?.Trim();
		// A registered connection (including identity) owns a misleading historic code.
		if (string.IsNullOrEmpty(binding) || !AdmissionFormBindingRegistry.Metadata.ContainsKey(binding))
		{
			if (AdmissionInquiryConfiguredFields.ReservedBinding(field.Code, binding) != null) binding = null;
			else binding = field.Code.ToUpperInvariant() switch
			{
				"SINGLE_CHILD" => "Family.IsSingleChild",
				"SIBLING_STUDENT" => "Family.SiblingStudentIds",
				_ => binding
			};
		}
		if (string.Equals(binding, "Family.IsSingleChild", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(binding, "Family.SiblingAtSchool", StringComparison.OrdinalIgnoreCase)) return "false";
		if (string.Equals(binding, "Family.SiblingStudentIds", StringComparison.OrdinalIgnoreCase)) return string.Empty;
		try
		{
			using var options = JsonDocument.Parse(field.OptionsJson ?? "{}");
			if (options.RootElement.ValueKind == JsonValueKind.Object && options.RootElement.TryGetProperty("source", out var source) &&
				source.ValueKind == JsonValueKind.String && source.GetString() == "StudentSiblings") return string.Empty;
		}
		catch (JsonException) { }
		return null;
	}

	private static string? ProjectRule(string? json, IReadOnlyDictionary<string, string> omitted)
	{
		if (string.IsNullOrWhiteSpace(json)) return json;
		try { return Fold(JsonNode.Parse(json))?.ToJsonString(); }
		catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException) { return json; }

		JsonNode? Fold(JsonNode? node)
		{
			if (node is not JsonObject condition) return node;
			if (condition["conditions"] is JsonArray children)
			{
				for (var index = 0; index < children.Count; index++) children[index] = Fold(children[index]?.DeepClone());
				return condition;
			}
			if (condition["field"] is not JsonValue fieldNode || !fieldNode.TryGetValue<string>(out var code) || !omitted.TryGetValue(code, out var value)) return condition;
			var matches = EvaluateOmitted(condition, code, value);
			return new JsonObject { ["operator"] = matches ? "ALL" : "ANY", ["conditions"] = new JsonArray() };
		}
	}

	private static bool EvaluateOmitted(JsonObject condition, string code, string value)
	{
		var operation = condition["operator"]?.GetValue<string>()?.ToLowerInvariant();
		if (value.Length == 0 && operation is "greaterthan" or "greaterthanorequal" or "lessthan" or "lessthanorequal")
		{
			// FE Number('') is zero. Restrict this compatibility rule to omitted list leaves;
			// ordinary staff and runtime numeric evaluation keeps its existing contract.
			var expected = JavaScriptText(condition["value"]);
			if (!TryNumber(expected, out var number)) return false;
			return operation switch
			{
				"greaterthan" => 0 > number,
				"greaterthanorequal" => 0 >= number,
				"lessthan" => 0 < number,
				_ => 0 <= number
			};
		}
		return new AdmissionWorkflowConditionEvaluator().Evaluate(condition.ToJsonString(), new Dictionary<string, string> { [code] = value });
	}

	private static string JavaScriptText(JsonNode? value)
	{
		var text = value switch
		{
			null => string.Empty,
			JsonValue scalar when scalar.TryGetValue<string>(out var str) => str,
			JsonArray array => string.Join(",", array.Select(JavaScriptText)),
			JsonObject => "[object Object]",
			_ => value.ToJsonString()
		};
		if (text.StartsWith('"')) text = text[1..];
		if (text.EndsWith('"')) text = text[..^1];
		return text;
	}

	private static bool TryNumber(string text, out double number)
	{
		text = text.Trim();
		if (text.Length == 0) { number = 0; return true; }
		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && ulong.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
		{
			number = hex; return true;
		}
		return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
	}

	private static BusinessException InvalidSnapshot() => new(AdmissionFormInheritanceErrorCodes.InvalidSnapshot,
		"The captured public Inquiry form cannot be read. Review the Inquiry before starting Registration.");

	private sealed class CapturedSource
	{
		public int FormatVersion { get; set; } = 1;
		public Guid? TenantId { get; set; }
		public AdmissionFormDto Definition { get; set; } = new();
	}
}
