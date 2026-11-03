using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionStructuredNamePrefill
{
	internal static string? FirstName(string current, IReadOnlyDictionary<string, string> inquiryValues)
	{
		if (Read(inquiryValues, ["Applicant.FirstName", "STUDENT_FIRST_NAME", "APPLICANT_FIRST_NAME", "FIRST_NAME"]) != null)
			return current;
		var fullName = Read(inquiryValues, ["Applicant.Name", "STUDENT_NAME", "APPLICANT_NAME", "CHILD_NAME"]);
		return fullName != null && string.Equals(current.Trim(), fullName, StringComparison.Ordinal) ? null : current;
	}

	private static string? Read(IReadOnlyDictionary<string, string> values, string[] keys)
	{
		foreach (var key in keys)
		{
			var pair = values.FirstOrDefault(value => string.Equals(value.Key, key, StringComparison.OrdinalIgnoreCase));
			if (pair.Value == null) continue;
			try
			{
				using var json = JsonDocument.Parse(pair.Value);
				if (json.RootElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(json.RootElement.GetString()))
					return json.RootElement.GetString()!.Trim();
			}
			catch (JsonException) { }
		}
		return null;
	}
}
