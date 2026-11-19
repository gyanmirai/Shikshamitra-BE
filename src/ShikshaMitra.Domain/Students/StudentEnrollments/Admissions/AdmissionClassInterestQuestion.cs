using System;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>One class-interest question, separate from final placement and previous schooling.</summary>
public static class AdmissionClassInterestQuestion
{
	public const string Identity = "academic:class-interest";
	public const string DuplicateMessage = "Keep only one Class interested in question. Reuse the existing question.";
	public const string ConfigurationMessage = "Class interested in must use the Institute Setup class dropdown.";

	public static bool Matches(string code, string label, AdmissionFormBindingType bindingType, string? bindingKey, string? optionsJson = null)
	{
		if (bindingType == AdmissionFormBindingType.System || bindingKey?.Contains('.') == true) return false;
		try
		{
			using var options = JsonDocument.Parse(optionsJson ?? "{}");
			if (options.RootElement.ValueKind == JsonValueKind.Object && options.RootElement.TryGetProperty("source", out var source) &&
				source.ValueKind == JsonValueKind.String && source.GetString() is "EducationBoards" or "EducationMediums") return false;
		}
		catch (JsonException) { }
		var separator = code.LastIndexOf('_');
		var reservedCode = separator > 0 && int.TryParse(code[(separator + 1)..], out _) ? code[..separator] : code;
		return Alias(reservedCode) || Alias(label);
	}

	public static bool IsConfigured(AdmissionFormFieldType type, string? options)
	{
		if (type != AdmissionFormFieldType.Select) return false;
		try
		{
			using var json = JsonDocument.Parse(options ?? "{}");
			return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("source", out var source) &&
				source.ValueKind == JsonValueKind.String && (source.GetString() == "Standards" ||
					source.GetString() == "StandardsByMedium" && json.RootElement.TryGetProperty("parentField", out var parent) &&
					parent.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(parent.GetString()));
		}
		catch (JsonException) { return false; }
	}

	private static bool Alias(string value) => new string(value.Where(character => !char.IsWhiteSpace(character) && character is not ('_' or '.' or '-')).ToArray()).ToUpperInvariant()
		is "CLASSINTERESTED" or "CLASSINTERESTEDIN" or "INTERESTEDCLASS";
}
