using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Maps the selected first parent's legacy form aliases without changing a shared sibling contact.</summary>
internal static class AdmissionFirstParentContactMapping
{
	private const string StructuredPrefix = "ParentAccountStructuredContact.";
	private const string AliasPrefix = "ParentAccountAliasContact.";

	public static bool CanMap(AdmissionApplicant applicant) =>
		(AdmissionSiblingParentSelection.IsParentFlow(applicant) || AdmissionPrimaryContactSelection.HasSelection(applicant)) && !applicant.ExistingStudentId.HasValue &&
		AdmissionSiblingParentSelection.ReadConfirmation(applicant) == null &&
		AdmissionSiblingParentSelection.IsRelationship(ParentRole(applicant));

	public static Dictionary<string, string> ReadInquiryAnswers(AdmissionInquiry inquiry, IEnumerable<AdmissionFormFieldDefinition> definitions)
	{
		var answers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(inquiry.FormResponseSnapshotJson)) return answers;
		var snapshot = JsonSerializer.Deserialize<Dictionary<string, string>>(inquiry.FormResponseSnapshotJson) ?? [];
		foreach (var field in definitions.Where(field => field.BindingType == AdmissionFormBindingType.System && field.BindingKey != null))
			if (snapshot.TryGetValue(field.BindingKey!, out var value)) answers[field.BindingKey!] = AdmissionApplicationValueConverter.Unwrap(value);
		return answers;
	}

	public static void Apply(AdmissionApplicant applicant, IReadOnlyDictionary<string, string> answers,
		AdmissionGuardian? guardian = null, bool recoverSavedAnswers = false)
	{
		if (!CanMap(applicant)) return;
		if (recoverSavedAnswers && AdmissionPrimaryContactSelection.HasSelection(applicant)) return;
		var prefix = ParentRole(applicant) == "father" ? "Father" : "Mother";
		foreach (var suffix in new[] { "Name", "Mobile", "Email" })
		{
			var key = prefix + "." + suffix;
			if (!answers.TryGetValue(key, out var structured)) continue;
			// A saved explicit clear owns the field too. Never restore an older form answer over a later edit.
			if (!recoverSavedAnswers || !applicant.GetProperty<bool>(StructuredPrefix + key))
				AdmissionAdditionalStudentFieldCatalog.ApplyValue(key, structured, applicant);
			applicant.SetProperty(StructuredPrefix + key, true);
		}
		if (CanApplyAlias("Email") && answers.TryGetValue("Guardian.Email", out var email))
			ApplyAlias("Email", AdmissionSiblingParentSelection.NormalizeEmail(email) ?? string.Empty);
		if (CanApplyAlias("Mobile") && answers.TryGetValue("Guardian.Mobile", out var mobile))
			ApplyAlias("Mobile", mobile);
		if (CanApplyAlias("Name") &&
			(answers.ContainsKey("Guardian.FirstName") || answers.ContainsKey("Guardian.LastName")))
		{
			var previous = (AdmissionStudentProfileMapping.Read(applicant, prefix + ".Name") ?? string.Empty)
				.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
			var firstName = answers.TryGetValue("Guardian.FirstName", out var first) ? first : guardian?.FirstName ?? previous.FirstOrDefault();
			var lastName = answers.TryGetValue("Guardian.LastName", out var last) ? last : guardian?.LastName ?? previous.ElementAtOrDefault(1);
			ApplyAlias("Name", string.Join(" ", new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value))));
		}

		bool CanApplyAlias(string suffix) => !applicant.GetProperty<bool>(StructuredPrefix + prefix + "." + suffix) &&
			(!recoverSavedAnswers || !applicant.GetProperty<bool>(AliasPrefix + prefix + "." + suffix));
		void ApplyAlias(string suffix, string value)
		{
			AdmissionAdditionalStudentFieldCatalog.ApplyValue(prefix + "." + suffix, value, applicant);
			applicant.SetProperty(AliasPrefix + prefix + "." + suffix, true);
		}
	}

	private static string? ParentRole(AdmissionApplicant applicant) => AdmissionSiblingParentSelection.ReadRelationship(applicant) ?? AdmissionPrimaryContactSelection.ReadRelationship(applicant);
}
