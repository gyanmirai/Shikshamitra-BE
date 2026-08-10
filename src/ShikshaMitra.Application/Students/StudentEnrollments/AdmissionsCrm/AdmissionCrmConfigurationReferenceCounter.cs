using System;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmConfigurationReferenceCounter
{
	public static int Count(string code, params string?[] values) => values.Sum(value => string.IsNullOrWhiteSpace(value) ? 0 : CountJson(value, code));
	public static int CountExact(string code, string? value) => string.Equals(value?.Trim(), code, StringComparison.OrdinalIgnoreCase) ? 1 : 0;

	private static int CountJson(string json, string code)
	{
		try
		{
			using var document = JsonDocument.Parse(json);
			return CountElement(document.RootElement, code);
		}
		catch (JsonException)
		{
			return CountExact(code, json);
		}
	}

	private static int CountElement(JsonElement element, string code) => element.ValueKind switch
	{
		JsonValueKind.Object => element.EnumerateObject().Sum(x => CountExact(code, x.Name) + CountElement(x.Value, code)),
		JsonValueKind.Array => element.EnumerateArray().Sum(x => CountElement(x, code)),
		JsonValueKind.String => CountExact(code, element.GetString()),
		_ => 0
	};
}
