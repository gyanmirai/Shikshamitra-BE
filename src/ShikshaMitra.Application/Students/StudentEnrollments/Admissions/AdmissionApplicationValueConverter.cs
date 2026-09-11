using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionApplicationValueConverter
{
	public static string Unwrap(string? value)
	{
		if (string.IsNullOrWhiteSpace(value) || value == "null") return string.Empty;
		try
		{
			using var json = JsonDocument.Parse(value);
			return json.RootElement.ValueKind == JsonValueKind.String
				? json.RootElement.GetString() ?? string.Empty
				: json.RootElement.ToString();
		}
		catch (JsonException)
		{
			return value;
		}
	}
}
