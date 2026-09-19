using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal static class AdmissionInquiryIdentityFieldResolver
{
	private static readonly IReadOnlyDictionary<string, string[]> LegacyFieldCodes =
		new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			["Applicant.Name"] = ["STUDENT_NAME", "APPLICANT_NAME", "CHILD_NAME"],
			["Applicant.FirstName"] = ["STUDENT_FIRST_NAME", "APPLICANT_FIRST_NAME", "FIRST_NAME"],
			["Applicant.LastName"] = ["STUDENT_LAST_NAME", "APPLICANT_LAST_NAME", "LAST_NAME"],
			["Applicant.DateOfBirth"] = ["DATE_OF_BIRTH", "DOB", "STUDENT_DOB", "APPLICANT_DOB"],
			["Guardian.FirstName"] = ["GUARDIAN_FIRST_NAME", "PARENT_FIRST_NAME", "GUARDIAN_NAME", "PARENT_NAME"],
			["Guardian.LastName"] = ["GUARDIAN_LAST_NAME", "PARENT_LAST_NAME"],
			["Guardian.Mobile"] = ["GUARDIAN_MOBILE", "GUARDIAN_PHONE", "PARENT_MOBILE", "PARENT_PHONE"],
			["Guardian.Email"] = ["GUARDIAN_EMAIL", "PARENT_EMAIL"]
		};

	public static string? Resolve(
		IReadOnlyDictionary<string, string> values,
		IEnumerable<AdmissionFormFieldDefinition> fields,
		string bindingKey,
		string? fallback)
	{
		var fieldList = fields as IReadOnlyList<AdmissionFormFieldDefinition> ?? fields.ToList();
		var canonicalCode = fieldList.FirstOrDefault(field =>
			string.Equals(field.BindingKey, bindingKey, StringComparison.OrdinalIgnoreCase))?.Code;
		var canonicalValue = Value(values, canonicalCode);
		if (!string.IsNullOrWhiteSpace(canonicalValue)) return canonicalValue;

		if (LegacyFieldCodes.TryGetValue(bindingKey, out var aliases))
		{
			foreach (var alias in aliases)
			{
				var aliasCode = fieldList.FirstOrDefault(field =>
					string.Equals(field.Code, alias, StringComparison.OrdinalIgnoreCase))?.Code;
				var aliasValue = Value(values, aliasCode);
				if (!string.IsNullOrWhiteSpace(aliasValue)) return aliasValue;
			}
		}

		return string.IsNullOrWhiteSpace(fallback) ? fallback : fallback.Trim();
	}

	public static DateTime? ResolveDate(
		IReadOnlyDictionary<string, string> values,
		IEnumerable<AdmissionFormFieldDefinition> fields,
		string bindingKey,
		DateTime? fallback)
	{
		var value = Resolve(values, fields, bindingKey, null);
		if (string.IsNullOrWhiteSpace(value)) return fallback;
		return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces,
			out var dateTimeOffset)
			? dateTimeOffset.Date
			: fallback;
	}

	private static string? Value(IReadOnlyDictionary<string, string> values, string? code) =>
		code != null && values.TryGetValue(code, out var value) && !string.IsNullOrWhiteSpace(value)
			? value.Trim()
			: null;
}
