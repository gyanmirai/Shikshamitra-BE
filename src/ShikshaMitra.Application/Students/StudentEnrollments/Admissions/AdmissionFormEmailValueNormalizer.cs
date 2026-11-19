using System;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionFormEmailValueNormalizer
{
	public static string? Normalize(AdmissionFormFieldDefinition field, string? valueJson)
	{
		if (field.ReadOnly || !IsEmailField(field) || string.IsNullOrWhiteSpace(valueJson)) return valueJson;
		try
		{
			using var document = JsonDocument.Parse(valueJson);
			return document.RootElement.ValueKind switch
			{
				JsonValueKind.String => JsonSerializer.Serialize(NormalizeText(document.RootElement.GetString()!)),
				JsonValueKind.Array => NormalizeArray(document.RootElement),
				_ => valueJson
			};
		}
		catch (JsonException)
		{
			// Legacy scalar answers are accepted by the existing value converter.
			return valueJson.Trim().ToLowerInvariant();
		}
	}

	private static string NormalizeText(string value)
	{
		if (value.TrimStart().StartsWith('['))
		{
			try
			{
				using var nested = JsonDocument.Parse(value);
				if (nested.RootElement.ValueKind == JsonValueKind.Array) return NormalizeArray(nested.RootElement);
			}
			catch (JsonException) { }
		}
		return value.Trim().ToLowerInvariant();
	}

	private static string NormalizeArray(JsonElement array) => JsonSerializer.Serialize(array.EnumerateArray()
		.Select(item => item.ValueKind == JsonValueKind.String
			? (object?)item.GetString()!.Trim().ToLowerInvariant()
			: item.Clone()).ToArray());

	private static bool IsEmailField(AdmissionFormFieldDefinition field)
	{
		if (field.FieldType == AdmissionFormFieldType.Email) return true;
		if (string.IsNullOrWhiteSpace(field.ValidationJson)) return false;
		try
		{
			using var configuration = JsonDocument.Parse(field.ValidationJson);
			return configuration.RootElement.ValueKind == JsonValueKind.Object &&
				configuration.RootElement.TryGetProperty("format", out var format) &&
				format.ValueKind == JsonValueKind.String &&
				string.Equals(format.GetString(), "email", StringComparison.OrdinalIgnoreCase);
		}
		catch (JsonException) { return false; }
	}
}
