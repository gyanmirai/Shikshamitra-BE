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
	internal const string MissingSelector = "Add a required Primary contact selector when both Father and Mother contacts are configured.";
	internal const string InvalidSelector = "Configure a required Primary contact selector with Father, Mother, and Guardian choices before publishing.";
	internal const string MixedNames = "Use either Student name or separate student name fields before publishing.";
	internal const string ExplicitMapping = "Connect inquiry fields to their matching Student fields before publishing.";
	internal const string ConflictingEmail = "Show only one email field for the selected primary contact; keep the other parent's email separate.";
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
		// Publishing requires explicit system connections. Find/Read retain the code
		// aliases separately for forms that were already published by older clients.
		AdmissionFormFieldDefinition? Mapped(string key) => fields.FirstOrDefault(field =>
			field.BindingType == AdmissionFormBindingType.System && string.Equals(field.BindingKey, key, StringComparison.OrdinalIgnoreCase));
		var identityKeys = Aliases.Keys.Concat(new[] { "Inquiry.ParentRelationship", "Inquiry.Source", "Inquiry.SourceDetail", "Inquiry.PreferredLanguageCode" }).ToArray();
		if (fields.Any(field => identityKeys.Any(key => Find([field], key) != null) &&
			!(field.BindingType == AdmissionFormBindingType.System && identityKeys.Contains(field.BindingKey, StringComparer.OrdinalIgnoreCase))))
			issues.Add(ExplicitMapping);
		bool Safe(AdmissionFormFieldDefinition? field) => field != null && field.Required && !field.ReadOnly &&
			field.FieldType == (field.BindingKey!.EndsWith(".Mobile", StringComparison.OrdinalIgnoreCase) ? AdmissionFormFieldType.Phone :
				field.BindingKey.Equals("Inquiry.ParentRelationship", StringComparison.OrdinalIgnoreCase) ? AdmissionFormFieldType.Select : AdmissionFormFieldType.Text) &&
			Unconditional(field.VisibilityRuleJson) && repeatedSections?.Contains(field.SectionId) != true &&
			sections.Any(section => section.Id == field.SectionId && Unconditional(section.VisibilityRuleJson) && Unconditional(section.StepVisibilityRuleJson));
		bool Pair(string prefix) => Mapped(prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name") != null && Mapped(prefix + ".Mobile") != null;
		bool SafeName(string prefix) => Safe(Mapped(prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name"));
		bool SafePhone(string prefix) => Safe(Mapped(prefix + ".Mobile"));
		if (!Safe(Mapped("Applicant.FirstName")) && !Safe(Mapped("Applicant.Name"))) issues.Add(MissingStudent);
		if (Mapped("Applicant.Name") != null && new[] { "Applicant.FirstName", "Applicant.MiddleName", "Applicant.LastName" }.Any(key => Mapped(key) != null))
			issues.Add(MixedNames);
		var generic = Pair("Guardian");
		var father = Pair("Father");
		var mother = Pair("Mother");
		var selector = Mapped("Inquiry.ParentRelationship");
		var genericEmail = Mapped("Guardian.Email");
		if (genericEmail != null && selector != null)
			foreach (var (role, prefix) in new[] { ("father", "Father"), ("mother", "Mother") })
			{
				var ownEmail = Mapped(prefix + ".Email");
				if (ownEmail != null && VisibleForRole(genericEmail, selector.Code, role, sections) && VisibleForRole(ownEmail, selector.Code, role, sections))
					issues.Add(ConflictingEmail);
			}
		if (selector != null)
		{
			if (!Safe(selector) || selector.FieldType != AdmissionFormFieldType.Select || !HasPrimaryContactChoices(selector.OptionsJson) ||
				(HasGuardianChoice(selector.OptionsJson) && !(generic && SafeName("Guardian") && SafePhone("Guardian"))) ||
				!(generic && SafeName("Guardian") && SafePhone("Guardian") || SafeName("Father") && SafePhone("Father")) ||
				!(generic && SafeName("Guardian") && SafePhone("Guardian") || SafeName("Mother") && SafePhone("Mother")))
				issues.Add(InvalidSelector);
			// A selector owns each chosen contact path. Optional generic questions are not
			// another required primary contact when both structured paths are complete.
			return issues.Distinct().ToList();
		}
		if (generic)
		{
			if (father && mother) issues.Add(MissingSelector);
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
		IReadOnlyDictionary<string, string> answers, string? savedRelationship = null, bool allowLegacySplitContact = false,
		IReadOnlySet<string>? activeFieldCodes = null)
	{
		var evaluator = new AdmissionWorkflowConditionEvaluator();
		fields = fields.Where(field => (activeFieldCodes?.Contains(field.Code) ?? evaluator.Evaluate(field.VisibilityRuleJson, answers)) ||
			allowLegacySplitContact && field.ReadOnly && answers.ContainsKey(field.Code)).ToList();
		bool Pair(string prefix) => Find(fields, prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name") != null && Find(fields, prefix + ".Mobile") != null;
		bool RequiredPair(string prefix) => Find(fields, prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name")?.Required == true && Find(fields, prefix + ".Mobile")?.Required == true;
		bool Part(string prefix) => Find(fields, prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name") != null || Find(fields, prefix + ".Mobile") != null;
		bool OwnsIdentity(string key) => Find(fields, key) is { } field &&
			(!allowLegacySplitContact || field.Required || answers.ContainsKey(field.Code));
		AdmissionInquiryConfiguredContact Contact(string prefix, string? relationship) => new(relationship,
			Read(fields, answers, prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name"),
			prefix == "Guardian" ? Read(fields, answers, "Guardian.LastName") : null,
			Read(fields, answers, prefix + ".Mobile"),
			Find(fields, prefix + ".Email") != null ? Read(fields, answers, prefix + ".Email") : Read(fields, answers, "Guardian.Email"),
			!allowLegacySplitContact || Find(fields, "Inquiry.ParentRelationship") != null ||
				OwnsIdentity(prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name") || OwnsIdentity(prefix + ".Mobile"),
			Find(fields, prefix + ".Email") != null || Find(fields, "Guardian.Email") != null,
			prefix == "Guardian" && OwnsIdentity("Guardian.LastName"),
			prefix != "Guardian" && OwnsIdentity(prefix + ".Name"),
			OwnsIdentity(prefix == "Guardian" ? "Guardian.FirstName" : prefix + ".Name"),
			OwnsIdentity(prefix + ".Mobile"));
		var generic = Pair("Guardian");
		// Historical locked primary contact fields remain the owner during edits.
		if (generic && allowLegacySplitContact && Find(fields, "Guardian.Mobile")?.ReadOnly == true)
			return Contact("Guardian", savedRelationship);
		var selector = Find(fields, "Inquiry.ParentRelationship");
		if (selector != null)
		{
			var selected = Read(fields, answers, "Inquiry.ParentRelationship");
			if (!AdmissionPrimaryContactSelection.IsRelationship(selected)) return new(null, null, null, null, null, true);
			var prefix = selected == "father" ? "Father" : selected == "mother" ? "Mother" : "Guardian";
			if (generic && (selected == "guardian" || Find(fields, "Guardian.FirstName")?.Required == true && Find(fields, "Guardian.Mobile")?.Required == true))
			{
				var contact = Contact("Guardian", selected);
				return prefix != "Guardian" && Find(fields, prefix + ".Email") != null
					? contact with { Email = Read(fields, answers, prefix + ".Email"), EmailIsConfigured = true } : contact;
			}
			if (Part(prefix)) return Contact(prefix, selected);
			return Part("Guardian") ? Contact("Guardian", selected) : new(selected, null, null, null, null, true);
		}
		if (generic)
		{
			if (!RequiredPair("Guardian"))
			{
				if (AdmissionSiblingParentSelection.IsRelationship(savedRelationship))
				{
					var savedPrefix = savedRelationship == "father" ? "Father" : "Mother";
					return Pair(savedPrefix) ? Contact(savedPrefix, savedRelationship) : Contact("Guardian", savedRelationship);
				}
				if (savedRelationship != "guardian" && RequiredPair("Father") != RequiredPair("Mother"))
					return RequiredPair("Father") ? Contact("Father", "father") : Contact("Mother", "mother");
			}
			return Contact("Guardian", AdmissionPrimaryContactSelection.IsRelationship(savedRelationship) ? savedRelationship : null);
		}
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
					Find(fields, "Guardian.LastName") != null, false, true, true);
		}
		// Legacy generic capture resolves each Guardian binding independently and retains
		// its DTO identity fallback. Only edits need the partial contact ownership flags.
		if (Part("Guardian")) return allowLegacySplitContact
			? Contact("Guardian", AdmissionPrimaryContactSelection.IsRelationship(savedRelationship) ? savedRelationship : null)
			: new(null, null, null, null, null, false);
		if (AdmissionSiblingParentSelection.IsRelationship(savedRelationship) && Part(savedRelationship == "father" ? "Father" : "Mother"))
			return Contact(savedRelationship == "father" ? "Father" : "Mother", savedRelationship);
		var fatherPart = Part("Father");
		var motherPart = Part("Mother");
		if (fatherPart != motherPart) return Contact(fatherPart ? "Father" : "Mother", fatherPart ? "father" : "mother");
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
	internal static bool HasPrimaryContactChoices(string? options)
	{
		if (string.IsNullOrWhiteSpace(options)) return true; // Registry supplies the bounded default choices.
		try
		{
			using var json = JsonDocument.Parse(options);
			return json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() is 2 or 3 &&
				json.RootElement.EnumerateArray().Select(option => option.GetProperty("value").GetString()).ToHashSet() is var choices && (choices.SetEquals(["father", "mother"]) || choices.SetEquals(["father", "mother", "guardian"]));
		}
		catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) { return false; }
	}

	private static bool HasGuardianChoice(string? options)
	{
		if (string.IsNullOrWhiteSpace(options)) return true;
		try
		{
			using var json = JsonDocument.Parse(options);
			return json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.EnumerateArray().Any(option =>
				option.ValueKind == JsonValueKind.Object && option.TryGetProperty("value", out var value) && value.GetString() == "guardian");
		}
		catch (Exception error) when (error is JsonException or InvalidOperationException) { return false; }
	}

	private static bool VisibleForRole(AdmissionFormFieldDefinition field, string selectorCode, string role,
		IReadOnlyCollection<AdmissionFormSectionDefinition> sections)
	{
		var section = sections.FirstOrDefault(item => item.Id == field.SectionId);
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [selectorCode] = role };
		var evaluator = new AdmissionWorkflowConditionEvaluator();
		return Evaluate(field.VisibilityRuleJson) && Evaluate(section?.VisibilityRuleJson) && Evaluate(section?.StepVisibilityRuleJson);
		bool Evaluate(string? rule)
		{
			if (string.IsNullOrWhiteSpace(rule)) return true;
			try { return evaluator.Evaluate(rule, values); }
			catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) { return true; }
		}
	}
}

internal sealed record AdmissionInquiryConfiguredContact(string? Relationship, string? Name, string? LastName, string? Mobile, string? Email, bool IsConfigured,
	bool EmailIsConfigured = false, bool LastNameIsConfigured = false, bool WholeNameIsConfigured = false,
	bool NameIsConfigured = false, bool MobileIsConfigured = false);
