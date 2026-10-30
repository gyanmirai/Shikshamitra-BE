using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Inquiry identity comes from published bindings, never question labels or injected controls.</summary>
internal static class AdmissionInquiryConfiguredFields
{
	internal const string MissingStudent = "Add a required Student name field before publishing.";
	internal const string MissingParent = "Add a required Parent / Guardian name field before publishing.";
	internal const string MissingMobile = "Add a required Parent / Guardian mobile field before publishing.";
	internal const string MissingSelector = "Add a required Primary parent selector when both Father and Mother contacts are configured.";
	internal const string InvalidSelector = "Configure a required Primary parent selector with Father and Mother choices before publishing.";
	internal const string MixedNames = "Use either Student name or separate student name fields before publishing.";
	private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
	{
		["Applicant.Name"] = ["STUDENT_NAME", "APPLICANT_NAME", "CHILD_NAME"],
		["Applicant.FirstName"] = ["STUDENT_FIRST_NAME", "APPLICANT_FIRST_NAME", "FIRST_NAME"],
		["Applicant.MiddleName"] = ["STUDENT_MIDDLE_NAME", "APPLICANT_MIDDLE_NAME", "MIDDLE_NAME"],
		["Applicant.LastName"] = ["STUDENT_LAST_NAME", "APPLICANT_LAST_NAME", "LAST_NAME"],
		["Applicant.DateOfBirth"] = ["DATE_OF_BIRTH", "DOB", "STUDENT_DOB", "APPLICANT_DOB"],
		["Guardian.FirstName"] = ["GUARDIAN_FIRST_NAME", "PARENT_FIRST_NAME", "GUARDIAN_NAME", "PARENT_NAME"],
		["Guardian.LastName"] = ["GUARDIAN_LAST_NAME", "PARENT_LAST_NAME"],
		["Guardian.Mobile"] = ["GUARDIAN_MOBILE", "GUARDIAN_PHONE", "PARENT_MOBILE", "PARENT_PHONE"],
		["Guardian.Email"] = ["GUARDIAN_EMAIL", "PARENT_EMAIL"],
		["Father.Name"] = ["FATHER_NAME", "FATHER_FIRST_NAME"],
		["Father.Mobile"] = ["FATHER_MOBILE", "FATHER_PHONE"],
		["Father.Email"] = ["FATHER_EMAIL"],
		["Mother.Name"] = ["MOTHER_NAME", "MOTHER_FIRST_NAME"],
		["Mother.Mobile"] = ["MOTHER_MOBILE", "MOTHER_PHONE"],
		["Mother.Email"] = ["MOTHER_EMAIL"]
	};

	public static AdmissionFormFieldDefinition? Find(IEnumerable<AdmissionFormFieldDefinition> fields, string key)
	{
		var candidates = fields as IReadOnlyCollection<AdmissionFormFieldDefinition> ?? fields.ToList();
		return candidates.FirstOrDefault(field => string.Equals(field.BindingKey, key, StringComparison.OrdinalIgnoreCase)) ??
			(Aliases.TryGetValue(key, out var aliases) ? candidates.FirstOrDefault(field =>
				!HasOwnedBinding(field.BindingKey) && aliases.Any(alias => Normalize(alias) == Normalize(field.Code))) : null);
	}

	public static string? Read(IEnumerable<AdmissionFormFieldDefinition> fields, IReadOnlyDictionary<string, string> answers, string key)
	{
		var field = Find(fields, key);
		return field != null && answers.TryGetValue(field.Code, out var value) ? value.Trim() : null;
	}

	public static List<string> Validate(IReadOnlyCollection<AdmissionFormFieldDefinition> fields,
		IReadOnlyCollection<AdmissionFormSectionDefinition> sections, ISet<Guid>? repeatedSections = null)
	{
		var issues = new List<string>();
		bool Safe(AdmissionFormFieldDefinition? field) => field != null && field.Required && !field.ReadOnly &&
			Unconditional(field.VisibilityRuleJson) && repeatedSections?.Contains(field.SectionId) != true &&
			sections.Any(section => section.Id == field.SectionId && Unconditional(section.VisibilityRuleJson) && Unconditional(section.StepVisibilityRuleJson));
		bool Pair(string prefix) => Find(fields, prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name") != null && Find(fields, prefix + ".Mobile") != null;
		bool SafeName(string prefix) => Safe(Find(fields, prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name"));
		bool SafePhone(string prefix) => Safe(Find(fields, prefix + ".Mobile"));
		if (!Safe(Find(fields, "Applicant.FirstName")) && !Safe(Find(fields, "Applicant.Name"))) issues.Add(MissingStudent);
		if (Find(fields, "Applicant.Name") != null && new[] { "Applicant.FirstName", "Applicant.MiddleName", "Applicant.LastName" }.Any(key => Find(fields, key) != null))
			issues.Add(MixedNames);
		var generic = Pair("Guardian");
		var father = Pair("Father");
		var mother = Pair("Mother");
		var selector = Find(fields, "Inquiry.ParentRelationship");
		if (selector != null)
		{
			if (!Safe(selector) || selector.FieldType != AdmissionFormFieldType.Select || !HasBothParentChoices(selector.OptionsJson) ||
				!(father ? SafeName("Father") && SafePhone("Father") : generic && SafeName("Guardian") && SafePhone("Guardian")) ||
				!(mother ? SafeName("Mother") && SafePhone("Mother") : generic && SafeName("Guardian") && SafePhone("Guardian")))
				issues.Add(InvalidSelector);
			// A selector owns each chosen contact path. Optional generic questions are not
			// another required primary contact when both structured paths are complete.
			return issues.Distinct().ToList();
		}
		if (generic)
		{
			if (!SafeName("Guardian")) issues.Add(MissingParent);
			if (!SafePhone("Guardian")) issues.Add(MissingMobile);
		}
		else if (father || mother)
		{
			if (father && mother && selector == null) issues.Add(MissingSelector);
			if (father && !SafeName("Father") || mother && !SafeName("Mother")) issues.Add(MissingParent);
			if (father && !SafePhone("Father") || mother && !SafePhone("Mother")) issues.Add(MissingMobile);
		}
		else
		{
			issues.Add(MissingParent);
			issues.Add(MissingMobile);
		}
		return issues.Distinct().ToList();
	}

	public static AdmissionInquiryConfiguredContact ResolveContact(IReadOnlyCollection<AdmissionFormFieldDefinition> fields,
		IReadOnlyDictionary<string, string> answers, string? savedRelationship = null, bool allowLegacySplitContact = false)
	{
		bool Pair(string prefix) => Find(fields, prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name") != null && Find(fields, prefix + ".Mobile") != null;
		AdmissionInquiryConfiguredContact Contact(string prefix, string? relationship) => new(relationship,
			Read(fields, answers, prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name"),
			prefix == "Guardian" ? Read(fields, answers, "Guardian.LastName") : null,
			Read(fields, answers, prefix + ".Mobile"),
			Find(fields, prefix + ".Email") != null ? Read(fields, answers, prefix + ".Email") : Read(fields, answers, "Guardian.Email"), true,
			Find(fields, prefix + ".Email") != null || Find(fields, "Guardian.Email") != null,
			prefix == "Guardian" && Find(fields, "Guardian.LastName") != null,
			prefix != "Guardian");
		var generic = Pair("Guardian");
		// Historical locked primary contact fields remain the owner during edits.
		if (generic && allowLegacySplitContact && Find(fields, "Guardian.Mobile")?.ReadOnly == true)
			return Contact("Guardian", savedRelationship);
		var selector = Find(fields, "Inquiry.ParentRelationship");
		if (selector != null)
		{
			var selected = Read(fields, answers, "Inquiry.ParentRelationship");
			if (!AdmissionSiblingParentSelection.IsRelationship(selected)) return new(null, null, null, null, null, true);
			var prefix = selected == "father" ? "Father" : "Mother";
			if (Pair(prefix)) return Contact(prefix, selected);
			return generic ? Contact("Guardian", selected) : new(selected, null, null, null, null, true);
		}
		if (generic) return Contact("Guardian", AdmissionSiblingParentSelection.IsRelationship(savedRelationship) ? savedRelationship : null);
		var father = Pair("Father");
		var mother = Pair("Mother");
		if (father != mother) return Contact(father ? "Father" : "Mother", father ? "father" : "mother");
		if (father && AdmissionSiblingParentSelection.IsRelationship(savedRelationship))
			return Contact(savedRelationship == "father" ? "Father" : "Mother", savedRelationship);
		if (allowLegacySplitContact && AdmissionSiblingParentSelection.IsRelationship(savedRelationship))
		{
			var prefix = savedRelationship == "father" ? "Father" : "Mother";
			if (Find(fields, "Guardian.FirstName") != null && Find(fields, prefix + ".Mobile") != null)
				return new(savedRelationship, Read(fields, answers, "Guardian.FirstName"), Read(fields, answers, "Guardian.LastName"),
					Read(fields, answers, prefix + ".Mobile"), Read(fields, answers, prefix + ".Email") ?? Read(fields, answers, "Guardian.Email"), true,
					Find(fields, prefix + ".Email") != null || Find(fields, "Guardian.Email") != null,
					Find(fields, "Guardian.LastName") != null);
		}
		return new(null, null, null, null, null, false);
	}

	public static void ApplyApplicantName(AdmissionApplicant applicant, IReadOnlyCollection<AdmissionFormFieldDefinition> fields,
		IReadOnlyDictionary<string, string> values)
	{
		var first = Read(fields, values, "Applicant.FirstName");
		var full = Read(fields, values, "Applicant.Name");
		if (!string.IsNullOrWhiteSpace(first))
			applicant.UpdateProfile(first,
				Find(fields, "Applicant.MiddleName") != null ? Read(fields, values, "Applicant.MiddleName") : applicant.MiddleName,
				Find(fields, "Applicant.LastName") != null ? Read(fields, values, "Applicant.LastName") : applicant.LastName,
				applicant.DateOfBirth, applicant.Gender, applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, applicant.PostalCode);
		else if (!string.IsNullOrWhiteSpace(full))
			applicant.UpdateProfile(full, null, null, applicant.DateOfBirth, applicant.Gender,
				applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, applicant.PostalCode);
	}

	public static void ApplyOperationalValues(IReadOnlyCollection<AdmissionFormFieldDefinition> fields,
		IReadOnlyDictionary<string, string> values, CreateAdmissionInquiryDto input, bool isPublic)
	{
		if (!isPublic && Find(fields, "Inquiry.Source") != null)
		{
			var source = Read(fields, values, "Inquiry.Source");
			if (!string.IsNullOrWhiteSpace(source))
			{
				if (!int.TryParse(source, out var raw) || !Enum.IsDefined(typeof(AdmissionInquirySource), raw))
					throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Choose a valid inquiry source.");
				input.Source = (AdmissionInquirySource)raw;
			}
		}
		if (!isPublic && Find(fields, "Inquiry.SourceDetail") is { } sourceDetail && values.ContainsKey(sourceDetail.Code))
		{
			input.SourceDetail = Read(fields, values, "Inquiry.SourceDetail");
			if (input.SourceDetail?.Length > 128) throw new BusinessException(AdmissionInquiryManagementErrorCodes.InvalidInput, "Source details must be 128 characters or fewer.");
		}
		if (Find(fields, "Inquiry.PreferredLanguageCode") != null)
		{
			var language = Read(fields, values, "Inquiry.PreferredLanguageCode");
			if (!string.IsNullOrWhiteSpace(language)) input.PreferredLanguageCode = language;
		}
	}

	private static bool HasOwnedBinding(string? key) => key != null && AdmissionFormBindingRegistry.Metadata.ContainsKey(key);
	private static string Normalize(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
	private static bool Unconditional(string? rule) => string.IsNullOrWhiteSpace(rule);
	private static bool HasBothParentChoices(string? options)
	{
		if (string.IsNullOrWhiteSpace(options)) return true; // Registry supplies the bounded default choices.
		try
		{
			using var json = JsonDocument.Parse(options);
			return json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() == 2 &&
				json.RootElement.EnumerateArray().Select(option => option.GetProperty("value").GetString()).ToHashSet().SetEquals(["father", "mother"]);
		}
		catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) { return false; }
	}
}

internal sealed record AdmissionInquiryConfiguredContact(string? Relationship, string? Name, string? LastName, string? Mobile, string? Email, bool IsConfigured,
	bool EmailIsConfigured = false, bool LastNameIsConfigured = false, bool WholeNameIsConfigured = false);
