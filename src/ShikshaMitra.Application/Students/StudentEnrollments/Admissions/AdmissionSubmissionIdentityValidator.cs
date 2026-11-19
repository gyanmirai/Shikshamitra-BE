using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Checks the saved identity at submission without turning partial drafts into invalid writes.</summary>
internal static class AdmissionSubmissionIdentityValidator
{
	internal static bool IsOptionalNamePart(AdmissionFormFieldDefinition field) =>
		field.BindingType == AdmissionFormBindingType.System &&
		field.BindingKey is "Applicant.MiddleName" or "Applicant.LastName";

	internal static string MissingDetails(AdmissionApplicant applicant) =>
		"Complete the following identity details: " + string.Join(", ", Requirements(applicant)
			.Where(rule => !rule.Present).Select(rule => rule.Label)) + ".";

	internal static void Validate(AdmissionApplicant applicant, IReadOnlyCollection<AdmissionFormFieldDefinition> fields,
		IReadOnlySet<string> visibleCodes, IReadOnlyDictionary<string, string> values, AdmissionFormValidationResult result)
	{
		foreach (var rule in Requirements(applicant))
		{
			var editable = fields.Where(field => field.BindingType == AdmissionFormBindingType.System && !field.ReadOnly &&
				visibleCodes.Contains(field.Code) && rule.Bindings.Contains(field.BindingKey)).ToList();
			foreach (var field in editable)
			{
				var value = values.GetValueOrDefault(field.Code, string.Empty);
				if (string.IsNullOrWhiteSpace(value)) AddFieldError(field, $"{field.Label} is required.");
				else if (field.BindingKey == "Applicant.Gender" && !StudentCoreIdentityRules.ReadGender(value).HasValue)
					AddFieldError(field, $"Select a supported {field.Label.ToLowerInvariant()}.");
				else if (field.BindingKey == "Applicant.DateOfBirth" &&
					!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
					AddFieldError(field, $"Enter a valid {field.Label.ToLowerInvariant()}.");
			}
			if (rule.Present) continue;
			if (editable.Count > 0)
				AddFieldError(editable[0], $"{rule.Label} is required. Review and save this answer before submitting.");
			else
				result.Errors.Add($"{rule.Label} is required. Update applicant identity or ask an administrator to make this question available before submitting.");
		}
		void AddFieldError(AdmissionFormFieldDefinition field, string message)
		{
			if (result.FieldErrors.TryAdd(field.Code, message)) result.Errors.Add(message);
		}
	}

	private static (string[] Bindings, string Label, bool Present)[] Requirements(AdmissionApplicant applicant) =>
	[
		(["Applicant.Name", "Applicant.FirstName"], "Student name", !string.IsNullOrWhiteSpace(applicant.FirstName)),
		(["Applicant.DateOfBirth"], "Date of birth", applicant.DateOfBirth.HasValue),
		(["Applicant.Gender"], "Gender", StudentCoreIdentityRules.ReadGender(applicant.Gender).HasValue)
	];
}
